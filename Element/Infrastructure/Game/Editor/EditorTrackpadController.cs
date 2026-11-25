using System;
using System.Collections.Generic;
using System.Reflection;
using Element.Application.Input;
using Element.Infrastructure.Platform;
using HarmonyLib;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Element.Infrastructure.Game.Editor;

/// <summary>
/// Two-finger pan and pinch zoom in the level editor on macOS. The game patches call
/// it around the editor's update; an error turns it off and the original controls stay.
/// </summary>
internal sealed class EditorTrackpadController : IDisposable
{
  private readonly Func<bool> _enabled;
  private readonly Action<string, Exception> _logException;
  private readonly MacTrackpadNative _native;
  private readonly TrackpadEvent[] _buffer = new TrackpadEvent[4096];
  private readonly List<TrackpadEvent> _pending = new();
  private readonly List<RaycastResult> _hits = new();
  private readonly TrackpadGesture _scroll = new();
  private readonly TrackpadGesture _pinch = new();
  private readonly TrackpadGesture _rotation = new();
  private ITrackpadViewport _viewport;
  private readonly FieldInfo _popup = AccessTools.Field(typeof(scnEditor), "showingPopup");
  private readonly MethodInfo _dragCamera = AccessTools.Method(typeof(scnEditor), "DragCamera");
  private int _frame = -1;
  private scnEditor _editor;
  private Vector3? _zoomPointer;
  private bool _active,
    _suppressEditor,
    _suppressUi,
    _disposed;

  /// <summary>Null off macOS, or when the editor or the native library changed; the original controls stay.</summary>
  /// <param name="enabled">The player's trackpad setting.</param>
  public static EditorTrackpadController TryCreate(
    string runtimePath,
    Func<bool> enabled,
    Action<string> log,
    Action<string, Exception> logException
  )
  {
    if (UnityEngine.Application.platform != RuntimePlatform.OSXPlayer)
      return null;
    EditorTrackpadController controller = null;
    try
    {
      controller = new EditorTrackpadController(runtimePath, enabled, logException);
      if (controller._popup == null || controller._dragCamera == null)
        throw new MissingMemberException("Editor camera API changed");
      log("macOS editor trackpad ready: two-finger pan and pinch zoom.");
      return controller;
    }
    catch (Exception exception)
    {
      controller?.Dispose();
      logException("Trackpad unavailable; keeping original controls", exception);
      return null;
    }
  }

  private EditorTrackpadController(string runtimePath, Func<bool> enabled, Action<string, Exception> logException)
  {
    _enabled = enabled;
    _logException = logException;
    _native = new MacTrackpadNative(runtimePath);
  }

  public void Tick()
  {
    if (_disposed)
      return;
    try
    {
      PrepareFrame();
    }
    catch (Exception exception)
    {
      Fail(exception);
    }
  }

  private bool CanNavigate(scnEditor editor) =>
    !Element.Application.Input.InputGate.BlocksPointerInput
    && _enabled()
    && editor != null
    && !editor.playMode
    && editor.camera != null
    && editor.eventSystem != null
    && editor.eventSystem.currentInputModule is CustomStandaloneInputModule
    && !editor.userIsEditingAnInputField
    && !(bool)_popup.GetValue(editor)
    && !editor.prefsContainer.gameObject.activeInHierarchy
    && !editor.particleEditorContainer.gameObject.activeInHierarchy
    && !UnityEngine.Input.GetMouseButton(0)
    && !UnityEngine.Input.GetMouseButton(1)
    && !UnityEngine.Input.GetMouseButton(2);

  private void PrepareFrame()
  {
    if (_frame == Time.frameCount)
      return;
    _frame = Time.frameCount;
    _suppressEditor = _suppressUi = false;
    _pending.Clear();
    scnEditor editor = scnEditor.instance;
    var viewport = InputGate.TrackpadViewport;
    if (viewport != null && !_enabled())
      viewport = null;
    scnEditor target = viewport == null && CanNavigate(editor) ? editor : null;
    bool active = viewport != null || target != null;
    if (active != _active || target != _editor || viewport != _viewport)
    {
      // Scene/navigation transitions must not reuse a prior gesture's ownership.
      _scroll.Reset();
      _pinch.Reset();
      _rotation.Reset();
      _viewport = viewport;
      _editor = target;
      _active = active;
      _native.SetEnabled(false);
      _native.SetEnabled(active, viewport != null);
      return;
    }
    // Outside the editor this runs every frame; an unchanged inactive state
    // needs no native calls or fresh gesture state.
    if (!active)
      return;
    int count = _native.Read(_buffer);
    if (count < 0)
    {
      _scroll.Cancel();
      _pinch.Cancel();
      _rotation.Cancel();
      viewport?.SuppressWheel();
      _suppressEditor = _suppressUi = true;
      return;
    }
    for (int i = 0; i < count; i++)
    {
      TrackpadEvent sample = _buffer[i];
      if (sample.Kind == 3)
      {
        _scroll.Cancel();
        _pinch.Cancel();
        _rotation.Cancel();
        _pending.Clear();
        continue;
      }
      if (sample.Kind == 1)
        _suppressEditor = true;
      if (sample.Kind == 4 && viewport == null)
        continue;
      TrackpadGesture gesture =
        sample.Kind == 1 ? _scroll
        : sample.Kind == 4 ? _rotation
        : _pinch;
      bool canvas = gesture.Route(
        sample.Phase,
        sample.Momentum,
        () =>
          viewport == null ? CanStartOnCanvas(sample) : viewport.Accepts(new Vector2((float)sample.X, (float)sample.Y))
      );
      if (sample.Kind == 1)
        viewport?.SuppressWheel();
      if (sample.Kind == 1 && gesture.BlocksUi)
        _suppressUi = true;
      if (!canvas)
        continue;
      if (viewport == null)
        _pending.Add(sample);
      else if (sample.Kind == 1)
        viewport.Orbit(new Vector2((float)sample.DeltaX, (float)sample.DeltaY));
      else if (sample.Kind == 4)
        viewport.Rotate((float)sample.DeltaX);
      else
        viewport.Magnify((float)sample.Magnification);
    }
  }

  private bool CanStartOnCanvas(TrackpadEvent sample)
  {
    if (sample.X < 0 || sample.X > 1 || sample.Y < 0 || sample.Y > 1)
      return false;
    var pointer = new PointerEventData(_editor.eventSystem)
    {
      position = new Vector2((float)sample.X * Screen.width, (float)sample.Y * Screen.height),
    };
    _hits.Clear();
    _editor.eventSystem.RaycastAll(pointer, _hits);
    // Tiles/decoration physics hits are navigable; interactive Canvas UI is not.
    foreach (RaycastResult hit in _hits)
      if (hit.module is GraphicRaycaster)
        return false;
    return true;
  }

  private void Apply(scnEditor editor)
  {
    if (_disposed || editor != _editor || !CanNavigate(editor))
      return;
    try
    {
      foreach (TrackpadEvent sample in _pending)
      {
        if (sample.Kind == 1)
        {
          if (sample.DeltaX == 0 && sample.DeltaY == 0)
            continue;
          // AppKit deltas already honor natural scrolling. Convert the view
          // displacement through the camera so zoom and camera rotation agree.
          Camera camera = editor.camera;
          Vector3 origin = camera.ScreenToWorldPoint(Vector3.zero);
          Vector3 displacement =
            camera.ScreenToWorldPoint(
              new Vector3((float)sample.DeltaX * Screen.width, (float)sample.DeltaY * Screen.height, 0)
            ) - origin;
          _dragCamera.Invoke(editor, new object[] { camera.transform.position - displacement });
        }
        else if (editor.scrollSpeed > 0)
        {
          float target = (float)TrackpadGesture.ZoomSize(editor.camUserSizeMultiplier, sample.Magnification);
          float delta = (editor.camUserSizeMultiplier - target) / editor.scrollSpeed;
          if (delta != 0)
          {
            // Unity's mouse position can be stale while the window is unfocused.
            // Use the same native position for canvas routing and the zoom anchor.
            _zoomPointer = new Vector3((float)sample.X * Screen.width, (float)sample.Y * Screen.height, 0);
            try
            {
              editor.ZoomCamera(delta, anchorAtPointer: true, instant: true);
            }
            finally
            {
              _zoomPointer = null;
            }
          }
        }
      }
      _pending.Clear();
    }
    catch (Exception exception)
    {
      Fail(exception);
    }
  }

  private void Fail(Exception exception)
  {
    _logException("Trackpad disabled after an error; keeping original controls", exception);
    Dispose();
  }

  /// <summary>Before the editor's update: reads this frame's gestures.</summary>
  public void BeforeEditorUpdate() => Tick();

  /// <summary>After the editor's update: pans and zooms its camera.</summary>
  public void AfterEditorUpdate(scnEditor editor) => Apply(editor);

  /// <summary>The scroll the editor reads: none while a trackpad gesture moves the canvas.</summary>
  public Vector2 EditorScroll()
  {
    Tick();
    return !_disposed && _suppressEditor ? Vector2.zero : RDInput.mouseScrollDelta;
  }

  /// <summary>The scroll the editor's UI reads: none while a gesture belongs to the canvas.</summary>
  public void FilterUiScroll(ref Vector2 scroll)
  {
    Tick();
    if (!_disposed && _suppressUi)
      scroll = Vector2.zero;
  }

  /// <summary>Where a pinch zooms around; the mouse otherwise.</summary>
  public Vector3 ZoomPointer => _zoomPointer ?? UnityEngine.Input.mousePosition;

  public void Dispose()
  {
    if (_disposed)
      return;
    _disposed = true;
    _pending.Clear();
    _native.Dispose();
  }
}
