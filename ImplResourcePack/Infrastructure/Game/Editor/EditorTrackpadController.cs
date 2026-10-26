using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ImplResourcePack.Infrastructure.Game.Editor;

internal sealed class EditorTrackpadController : IDisposable
{
  private static EditorTrackpadController _current;
  private readonly Main _main;
  private readonly MacTrackpadNative _native;
  private readonly Harmony _harmony = new("ImplResourcePack.Trackpad");
  private readonly TrackpadEvent[] _buffer = new TrackpadEvent[4096];
  private readonly List<TrackpadEvent> _pending = new();
  private readonly List<RaycastResult> _hits = new();
  private readonly TrackpadGesture _scroll = new();
  private readonly TrackpadGesture _pinch = new();
  private readonly FieldInfo _popup = AccessTools.Field(typeof(scnEditor), "showingPopup");
  private readonly MethodInfo _dragCamera = AccessTools.Method(typeof(scnEditor), "DragCamera");
  private int _frame = -1;
  private scnEditor _editor;
  private Vector3? _zoomPointer;
  private bool _active, _suppressEditor, _suppressUi, _disposed;

  public static EditorTrackpadController TryCreate(Main main)
  {
    if (UnityEngine.Application.platform != RuntimePlatform.OSXPlayer) return null;
    EditorTrackpadController controller = null;
    try
    {
      controller = new EditorTrackpadController(main);
      controller.Install();
      return controller;
    }
    catch (Exception exception)
    {
      controller?.Dispose();
      main.LogException("Trackpad unavailable; keeping original controls", exception);
      return null;
    }
  }

  private EditorTrackpadController(Main main)
  {
    _main = main;
    _native = new MacTrackpadNative(main.Path);
  }

  private void Install()
  {
    if (_popup == null || _dragCamera == null) throw new MissingMemberException("Editor camera API changed");
    MethodInfo update = AccessTools.Method(typeof(scnEditor), "Update");
    _harmony.Patch(update,
      prefix: new HarmonyMethod(typeof(EditorTrackpadController), nameof(BeforeEditorUpdate)),
      postfix: new HarmonyMethod(typeof(EditorTrackpadController), nameof(AfterEditorUpdate)),
      transpiler: new HarmonyMethod(typeof(EditorTrackpadController), nameof(ReplaceEditorScroll)));
    _harmony.Patch(AccessTools.PropertyGetter(typeof(BaseInput), "mouseScrollDelta"),
      postfix: new HarmonyMethod(typeof(EditorTrackpadController), nameof(FilterUiScroll)));
    _harmony.Patch(AccessTools.Method(typeof(scnEditor), nameof(scnEditor.ZoomCamera)),
      transpiler: new HarmonyMethod(typeof(EditorTrackpadController), nameof(ReplaceZoomPointer)));
    _current = this;
    _main.Log("macOS editor trackpad ready: two-finger pan and pinch zoom.");
  }

  public void Tick()
  {
    if (_disposed) return;
    try { PrepareFrame(); }
    catch (Exception exception) { Fail(exception); }
  }

  private bool CanNavigate(scnEditor editor) =>
    !ImplResourcePack.Presentation.Settings.UIInteractionState.BlocksPointerInput &&
    _main.Settings.NativeEditorTrackpad &&
    editor != null && !editor.playMode && editor.camera != null &&
    editor.eventSystem != null && editor.eventSystem.currentInputModule is CustomStandaloneInputModule &&
    !editor.userIsEditingAnInputField && !(bool)_popup.GetValue(editor) &&
    !editor.prefsContainer.gameObject.activeInHierarchy &&
    !editor.particleEditorContainer.gameObject.activeInHierarchy &&
    !UnityEngine.Input.GetMouseButton(0) && !UnityEngine.Input.GetMouseButton(1) && !UnityEngine.Input.GetMouseButton(2);

  private void PrepareFrame()
  {
    if (_frame == Time.frameCount) return;
    _frame = Time.frameCount;
    _suppressEditor = _suppressUi = false;
    _pending.Clear();
    scnEditor editor = scnEditor.instance;
    bool active = CanNavigate(editor);
    scnEditor target = active ? editor : null;
    if (active != _active || target != _editor)
    {
      // Scene/navigation transitions must not reuse a prior gesture's ownership.
      _scroll.Reset();
      _pinch.Reset();
      _editor = target;
      _active = active;
      _native.SetEnabled(false);
      _native.SetEnabled(active);
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
        _pending.Clear();
        continue;
      }
      if (sample.Kind == 1) _suppressEditor = true;
      TrackpadGesture gesture = sample.Kind == 1 ? _scroll : _pinch;
      bool canvas = gesture.Route(sample.Phase, sample.Momentum, () => CanStartOnCanvas(sample));
      if (sample.Kind == 1 && gesture.BlocksUi) _suppressUi = true;
      if (!canvas) continue;
      _pending.Add(sample);
    }
  }

  private bool CanStartOnCanvas(TrackpadEvent sample)
  {
    if (sample.X < 0 || sample.X > 1 || sample.Y < 0 || sample.Y > 1) return false;
    var pointer = new PointerEventData(_editor.eventSystem)
    {
      position = new Vector2((float)sample.X * Screen.width, (float)sample.Y * Screen.height)
    };
    _hits.Clear();
    _editor.eventSystem.RaycastAll(pointer, _hits);
    // Tiles/decoration physics hits are navigable; interactive Canvas UI is not.
    foreach (RaycastResult hit in _hits)
      if (hit.module is GraphicRaycaster) return false;
    return true;
  }

  private void Apply(scnEditor editor)
  {
    if (_disposed || editor != _editor || !CanNavigate(editor)) return;
    try
    {
      foreach (TrackpadEvent sample in _pending)
      {
        if (sample.Kind == 1)
        {
          if (sample.DeltaX == 0 && sample.DeltaY == 0) continue;
          // AppKit deltas already honor natural scrolling. Convert the view
          // displacement through the camera so zoom and camera rotation agree.
          Camera camera = editor.camera;
          Vector3 origin = camera.ScreenToWorldPoint(Vector3.zero);
          Vector3 displacement = camera.ScreenToWorldPoint(new Vector3(
            (float)sample.DeltaX * Screen.width, (float)sample.DeltaY * Screen.height, 0)) - origin;
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
            try { editor.ZoomCamera(delta, anchorAtPointer: true, instant: true); }
            finally { _zoomPointer = null; }
          }
        }
      }
      _pending.Clear();
    }
    catch (Exception exception) { Fail(exception); }
  }

  private void Fail(Exception exception)
  {
    _main.LogException("Trackpad disabled after an error; keeping original controls", exception);
    Dispose();
  }

  private static void BeforeEditorUpdate() => _current?.Tick();
  private static void AfterEditorUpdate(scnEditor __instance) => _current?.Apply(__instance);
  private static Vector2 ReadEditorScroll()
  {
    _current?.Tick();
    return _current?._suppressEditor == true ? Vector2.zero : RDInput.mouseScrollDelta;
  }
  private static void FilterUiScroll(ref Vector2 __result)
  {
    _current?.Tick();
    if (_current?._suppressUi == true) __result = Vector2.zero;
  }
  private static Vector3 ReadZoomPointer() => _current?._zoomPointer ?? UnityEngine.Input.mousePosition;

  private static IEnumerable<CodeInstruction> ReplaceZoomPointer(IEnumerable<CodeInstruction> instructions)
  {
    MethodInfo original = AccessTools.PropertyGetter(typeof(UnityEngine.Input), nameof(UnityEngine.Input.mousePosition));
    MethodInfo replacement = AccessTools.Method(typeof(EditorTrackpadController), nameof(ReadZoomPointer));
    int matches = 0;
    var result = new List<CodeInstruction>();
    foreach (CodeInstruction instruction in instructions)
    {
      if (instruction.Calls(original))
      {
        instruction.opcode = OpCodes.Call;
        instruction.operand = replacement;
        matches++;
      }
      result.Add(instruction);
    }
    if (matches != 1) throw new InvalidOperationException("Editor zoom hook changed; expected exactly one pointer read");
    return result;
  }
  private static IEnumerable<CodeInstruction> ReplaceEditorScroll(IEnumerable<CodeInstruction> instructions)
  {
    MethodInfo original = AccessTools.PropertyGetter(typeof(RDInput), nameof(RDInput.mouseScrollDelta));
    MethodInfo replacement = AccessTools.Method(typeof(EditorTrackpadController), nameof(ReadEditorScroll));
    int matches = 0;
    var result = new List<CodeInstruction>();
    foreach (CodeInstruction instruction in instructions)
    {
      if (instruction.Calls(original))
      {
        instruction.opcode = OpCodes.Call;
        instruction.operand = replacement;
        matches++;
      }
      result.Add(instruction);
    }
    if (matches != 1) throw new InvalidOperationException("Editor scroll hook changed; expected exactly one call");
    return result;
  }

  public void Dispose()
  {
    if (_disposed) return;
    _disposed = true;
    if (_current == this) _current = null;
    _pending.Clear();
    _harmony.UnpatchAll(_harmony.Id);
    _native.Dispose();
  }
}
