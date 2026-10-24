using System;
using System.Collections.Generic;
using ImplResourcePack.Configuration;
using ImplResourcePack.Domain.Layout;
using ImplResourcePack.Presentation.Overlay.Shared;
using ImplResourcePack.Presentation.Settings;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ImplResourcePack.Presentation.Layout;

internal sealed partial class OverlayLayoutEditor : IDisposable
{
  private readonly OverlayCanvasHost _host;
  private readonly List<IOverlayLayoutTarget> _extraTargets = new();
  private readonly List<TargetFrame> _frames = new();
  private LayoutSnapshot _openingLayout;
  private string _openingColor;
  private readonly Func<string, IOverlayLayoutTarget> _addExternalElement;
  private readonly IEnumerable<IOverlayLayoutTarget> _galleryExtras;
  private readonly TMP_FontAsset _font;
  private readonly Action<bool> _onClosed;
  private readonly LayoutCursors _cursors = new();
  private GameObject _root;
  private RectTransform _toolbar;
  private TextMeshProUGUI _instruction;
  private TextMeshProUGUI _keyboardHelp;
  private int _selected = -1;
  private int _dragging = -1;
  private LayoutHandle _dragHandle;
  private Rect _dragStart;
  private Vector2 _pointerStart;
  private float _dragScale;
  private bool _previousCursorVisible;
  private CursorLockMode _previousCursorLock;
  private int _themeVersion = -1;

  public bool IsOpen { get; private set; }

  public OverlayLayoutEditor(
    OverlayCanvasHost host,
    IEnumerable<IOverlayLayoutTarget> extras,
    TMP_FontAsset font,
    Material material,
    Action<bool> onClosed,
    ImplResourcePackSettings preferences,
    Func<string, IOverlayLayoutTarget> addExternalElement = null,
    IEnumerable<IOverlayLayoutTarget> galleryExtras = null
  )
  {
    _host = host;
    _font = font;
    _onClosed = onClosed;
    _preferences = preferences;
    _addExternalElement = addExternalElement;
    _galleryExtras = galleryExtras;
    if (extras != null)
      _extraTargets.AddRange(extras);
  }

  public void Open()
  {
    if (IsOpen)
      return;
    EnsureRoot();
    _viewportWidth = Screen.width;
    _viewportHeight = Screen.height;
    EnsureEvents();
    RebuildFrames();
    _openingLayout = new LayoutSnapshot(_host.LayoutSettings);
    _openingColor = _preferences.HighlightColor;
    _previousCursorVisible = Cursor.visible;
    _previousCursorLock = Cursor.lockState;
    Cursor.visible = true;
    Cursor.lockState = CursorLockMode.None;
    IsOpen = true;
    _host.SetLayoutPreview(true);
    Canvas.ForceUpdateCanvases();
    _host.ApplyLayouts();
    foreach (TargetFrame frame in _frames)
    {
      frame.Target.SetPreview(true);
      frame.InitialBounds = frame.Target.Bounds;
    }
    _root.SetActive(true);
    _history.Reset(_host.LayoutSettings);
    _selected = _dragging = -1;
    _toolbarDragging = false;
    _guideX = _guideY = float.NaN;
    _horizontalSpacing = _verticalSpacing = default;
    _scaleErrorUntil = 0;
    UpdateFrames();
  }

  public void Close(bool save)
  {
    if (!IsOpen)
      return;
    if (save && _colorPopover != null && !_colorPopover.Commit())
      return;
    if (save && HasSelection && _scalePending)
    {
      CommitScale(_scaleInput.text);
      if (Time.unscaledTime < _scaleErrorUntil)
      {
        _scaleInput.Select();
        return;
      }
      _scaleInput.DeactivateInputField();
    }
    if (save)
      FinishDrag();
    IsOpen = false;
    _closing = true;
    _dragging = -1;
    _toolbarDragging = false;
    if (!save)
    {
      _openingLayout.Restore(_host.LayoutSettings);
      _preferences.HighlightColor = _openingColor;
      OverlayTheme.SetHighlight(_openingColor);
      RefreshLayouts();
    }
    foreach (TargetFrame frame in _frames)
    {
      (frame.Target as IExternalLayoutOutline)?.SetSelected(false);
      frame.Target.SetPreview(false);
    }
    _host.SetLayoutPreview(false);
    CloseGallery();
    _colorPopover?.Hide();
    _root.SetActive(false);
    _cursors.Set(LayoutHandle.None);
    Cursor.visible = _previousCursorVisible;
    Cursor.lockState = _previousCursorLock;
    UIInteractionState.SetTextEditing(false);
    if (_ownedEvents != null)
    {
      _ownedEvents.SetActive(false);
      SettingsUiFactory.Destroy(_ownedEvents);
      _ownedEvents = null;
    }
    _closing = false;
    _onClosed?.Invoke(save);
  }

  public void Tick()
  {
    if (!IsOpen)
      return;
    _horizontalSpacing = _verticalSpacing = default;
    EnsureEvents();
    bool typing = (_scaleInput != null && _scaleInput.isFocused) || (_colorPopover?.IsTyping ?? false);
    if (_viewportWidth != Screen.width || _viewportHeight != Screen.height)
      Resize(Screen.width, Screen.height);
    UIInteractionState.SetTextEditing(typing || _galleryRoot != null || (_colorPopover?.Visible ?? false));
    UpdatePopupPosition();
    if (_galleryRoot != null || (_colorPopover?.Visible ?? false))
    {
      if (Input.GetKeyDown(KeyCode.Escape))
      {
        if (_galleryRoot != null)
          CloseGallery();
        else
          _colorPopover.Hide();
      }
      else if (!typing && _colorPopover != null && _colorPopover.Visible && Input.GetMouseButtonDown(0))
      {
        Vector2 p = new(Input.mousePosition.x, Screen.height - Input.mousePosition.y);
        if (!ColorPopoverBounds.Contains(p) && !ToolbarBounds.Contains(p))
          _colorPopover.Hide();
      }
      _host.TickLayoutPreview();
      UpdateFrames();
      _cursors.Set(LayoutHandle.None);
      return;
    }
    if (typing)
    {
      _host.TickLayoutPreview();
      UpdateFrames();
      return;
    }
    if (Input.GetKeyDown(KeyCode.Escape))
    {
      Close(false);
      return;
    }
    if (CommandModifierHeld() && (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)))
    {
      Close(true);
      return;
    }
    if (!UnityEngine.Application.isFocused)
    {
      FinishDrag();
      _toolbarDragging = false;
      _guideX = _guideY = float.NaN;
      UpdateGuides();
      _cursors.Set(LayoutHandle.None);
      return;
    }
    _host.TickLayoutPreview();
    Vector2 mouse = new(Input.mousePosition.x, Screen.height - Input.mousePosition.y);
    Rect toolbar = ToolbarBounds;
    if (_toolbarDragging)
    {
      MoveToolbar(mouse);
      if (!Input.GetMouseButton(0))
        _toolbarDragging = false;
      _cursors.Set(LayoutHandle.Move);
    }
    else if (_dragging >= 0)
    {
      TargetFrame frame = _frames[_dragging];
      if (frame.Target.Available && Input.GetMouseButton(0))
      {
        Vector2 pointer = PointerFor(frame.Target, mouse);
        LayoutBounds resized = OverlayLayoutGeometry.Drag(
          ToBounds(_dragStart),
          _dragHandle,
          pointer.x - _pointerStart.x,
          pointer.y - _pointerStart.y,
          Screen.width,
          Screen.height,
          Mathf.Min(1f, 0.2f / _dragScale),
          Mathf.Max(1f, 5f / _dragScale)
        );
        if (
          _dragHandle == LayoutHandle.Move
          && _preferences.LayoutSnapping
          && !Input.GetKey(KeyCode.LeftAlt)
          && !Input.GetKey(KeyCode.RightAlt)
          && (pointer - _pointerStart).sqrMagnitude > 9
        )
        {
          _snapReferences.Clear();
          for (int i = 0; i < _frames.Count; i++)
            if (i != _dragging && _frames[i].Target.Available && _frames[i].Target.Enabled)
              _snapReferences.Add(ToBounds(_frames[i].Target.Bounds));
          resized = LayoutSnap.Move(
            resized,
            _snapReferences,
            Screen.width,
            Screen.height,
            7 * UiScale,
            out _guideX,
            out _guideY,
            out _horizontalSpacing,
            out _verticalSpacing
          );
        }
        else
          _guideX = _guideY = float.NaN;
        frame.Target.SetBounds(ToRect(resized));
      }
      _cursors.Set(_dragHandle);
      if (!Input.GetMouseButton(0))
      {
        FinishDrag();
        _guideX = _guideY = float.NaN;
      }
    }
    else if (toolbar.Contains(mouse))
    {
      bool canDrag = !ToolbarControlAt(mouse);
      _cursors.Set(canDrag ? LayoutHandle.Move : LayoutHandle.None);
      if (canDrag && Input.GetMouseButtonDown(0))
        BeginToolbarDrag(mouse);
    }
    else
    {
      int hovered = FindTarget(mouse, out LayoutHandle handle);
      _cursors.Set(handle);
      if (hovered >= 0 && Input.GetMouseButtonDown(0))
      {
        _scalePending = false;
        _scaleErrorUntil = 0;
        _selected = _dragging = hovered;
        _dragHandle = handle;
        _dragStart = _frames[hovered].Target.Bounds;
        _pointerStart = PointerFor(_frames[hovered].Target, mouse);
        _dragScale = ValidScale(_frames[hovered].Target.EffectiveScale);
      }
      else if (Input.GetMouseButtonDown(0))
        _selected = -1;
    }
    HandleKeyboard();
    UpdateFrames();
  }

  public void Dispose()
  {
    _closing = true;
    // Disposal must not reopen settings through the normal close callback.
    if (IsOpen)
    {
      _openingLayout.Restore(_host.LayoutSettings);
      _preferences.HighlightColor = _openingColor;
      OverlayTheme.SetHighlight(_openingColor);
      RefreshLayouts();
      foreach (TargetFrame frame in _frames)
      {
        (frame.Target as IExternalLayoutOutline)?.SetSelected(false);
        frame.Target.SetPreview(false);
      }
      _host.SetLayoutPreview(false);
      Cursor.visible = _previousCursorVisible;
      Cursor.lockState = _previousCursorLock;
    }
    IsOpen = false;
    _cursors.Dispose();
    CloseGallery();
    _colorPopover?.Dispose();
    _ui?.Dispose();
    UIInteractionState.SetTextEditing(false);
    if (_ownedEvents != null)
      SettingsUiFactory.Destroy(_ownedEvents);
    if (_root != null)
      SettingsUiFactory.Destroy(_root);
    _root = null;
  }

  private void HandleKeyboard()
  {
    if (_dragging >= 0 || _toolbarDragging || _frames.Count == 0)
      return;
    // Ordinary rhythm keys must keep reaching live gameplay. Layout keyboard
    // manipulation needs an explicit command modifier while a level is running.
    if (IsGameplayActive() && !CommandModifierHeld())
      return;
    if (Input.GetKeyDown(KeyCode.Tab) && !Input.GetKey(KeyCode.LeftAlt) && !Input.GetKey(KeyCode.RightAlt))
    {
      int direction = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift) ? -1 : 1;
      for (int count = 0; count < _frames.Count; count++)
      {
        _selected = (_selected + direction + _frames.Count) % _frames.Count;
        if (_frames[_selected].Target.Available && _frames[_selected].Target.Enabled)
          break;
      }
    }
    if (CommandModifierHeld() && Input.GetKeyDown(KeyCode.Z))
    {
      if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
        Redo();
      else
        Undo();
      return;
    }
    if (CommandModifierHeld() && Input.GetKeyDown(KeyCode.Y))
    {
      Redo();
      return;
    }
    if (CommandModifierHeld() && (Input.GetKeyDown(KeyCode.Delete) || Input.GetKeyDown(KeyCode.Backspace)))
    {
      DeleteSelected();
      return;
    }
    if (!HasSelection)
      return;
    float step = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift) ? 10f : 1f;
    float dx =
      Input.GetKeyDown(KeyCode.LeftArrow) ? -step
      : Input.GetKeyDown(KeyCode.RightArrow) ? step
      : 0f;
    float dy =
      Input.GetKeyDown(KeyCode.UpArrow) ? -step
      : Input.GetKeyDown(KeyCode.DownArrow) ? step
      : 0f;
    if (dx != 0f || dy != 0f)
    {
      IOverlayLayoutTarget target = _frames[_selected].Target;
      target.SetBounds(
        ToRect(
          OverlayLayoutGeometry.Drag(ToBounds(target.Bounds), LayoutHandle.Move, dx, dy, Screen.width, Screen.height)
        )
      );
      target.CommitLayout(true);
    }
    bool grow =
      Input.GetKeyDown(KeyCode.Equals) || Input.GetKeyDown(KeyCode.Plus) || Input.GetKeyDown(KeyCode.KeypadPlus);
    bool shrink = Input.GetKeyDown(KeyCode.Minus) || Input.GetKeyDown(KeyCode.KeypadMinus);
    if (grow || shrink)
    {
      float ratio = grow ? 1.02f : 1f / 1.02f;
      if (step > 1f)
        ratio = grow ? 1.1f : 1f / 1.1f;
      ScaleBy(ratio);
    }
    if (dx != 0 || dy != 0 || grow || shrink)
      _history.Commit(_host.LayoutSettings);
  }

  private void FinishDrag()
  {
    if (_dragging < 0 || _dragging >= _frames.Count)
      return;
    _frames[_dragging].Target.CommitLayout(_dragHandle == LayoutHandle.Move);
    _dragging = -1;
    _history.Commit(_host.LayoutSettings);
  }

  private int FindTarget(Vector2 pointer, out LayoutHandle bestHandle)
  {
    // The selected rectangle owns its editing area, including where another
    // instance's compact content overlaps it. New copies must remain draggable.
    if (HasSelection)
    {
      IOverlayLayoutTarget selected = _frames[_selected].Target;
      Vector2 selectedPointer = PointerFor(selected, pointer);
      bestHandle = OverlayLayoutGeometry.HitTest(
        ToBounds(selected.Bounds),
        selectedPointer.x,
        selectedPointer.y,
        10 * UiScale
      );
      if (bestHandle != LayoutHandle.None)
        return _selected;
    }
    int best = -1;
    float bestArea = float.MaxValue;
    bestHandle = LayoutHandle.None;
    for (int index = 0; index < _frames.Count; index++)
    {
      IOverlayLayoutTarget target = _frames[index].Target;
      if (!target.Available || !target.Enabled)
        continue;
      Rect bounds = index == _selected ? target.Bounds : target.SelectionBounds;
      if (bounds.width <= 0 || bounds.height <= 0)
        continue;
      Vector2 localPointer = PointerFor(target, pointer);
      LayoutHandle handle = OverlayLayoutGeometry.HitTest(
        ToBounds(bounds),
        localPointer.x,
        localPointer.y,
        index == _selected ? 10 * UiScale : 0
      );
      if (index != _selected && handle != LayoutHandle.None)
        handle = LayoutHandle.Move;
      if (handle == LayoutHandle.None)
        continue;
      bool edge = handle != LayoutHandle.Move;
      bool bestEdge = bestHandle != LayoutHandle.None && bestHandle != LayoutHandle.Move;
      float area = bounds.width * bounds.height;
      if (best < 0 || (edge && !bestEdge) || (edge == bestEdge && area <= bestArea))
      {
        best = index;
        bestArea = area;
        bestHandle = handle;
      }
    }
    return best;
  }

  private static Vector2 PointerFor(IOverlayLayoutTarget target, Vector2 fallback) =>
    target is IOverlayLayoutPointer native ? native.PointerInLayoutSpace(fallback) : fallback;

  private void UpdateFrames()
  {
    for (int index = 0; index < _frames.Count; index++)
    {
      TargetFrame frame = _frames[index];
      bool selected = frame.Target.Available && frame.Target.Enabled && index == _selected && _galleryRoot == null;
      (frame.Target as IExternalLayoutOutline)?.SetSelected(selected);
      bool visible = selected && !(frame.Target is IExternalLayoutOutline external && external.OwnsSelectionOutline);
      frame.Outline.gameObject.SetActive(visible);
      if (!visible)
        continue;
      Rect bounds = frame.Target.Bounds;
      SetRect(frame.Outline.rectTransform, bounds);
      frame.Outline.SetScale(UiScale);
      Rect panel = NativeExclusionBounds;
      frame.Outline.SetExclusion(new Rect(panel.x - bounds.x, -panel.yMax + bounds.y, panel.width, panel.height));
    }
    UpdateToolbar();
    UpdateGuides();
    if (_colorPopover?.Visible ?? false)
      _colorPopover.Root.SetAsLastSibling();
    if (_galleryRoot != null)
      _galleryRoot.transform.SetAsLastSibling();
  }

  private void RebuildFrames()
  {
    foreach (TargetFrame frame in _frames)
    {
      (frame.Target as IExternalLayoutOutline)?.SetSelected(false);
      SettingsUiFactory.Destroy(frame.Outline.gameObject);
    }
    _frames.Clear();
    foreach (IOverlayLayoutTarget target in _host.LayoutTargets)
      AddFrame(target);
    foreach (IOverlayLayoutTarget target in _extraTargets)
      AddFrame(target);
    _toolbar.SetAsLastSibling();
  }

  private void AddFrame(IOverlayLayoutTarget target)
  {
    GameObject root = OverlayTextFactory.CreateRect(
      "Layout: " + target.Id,
      _root.transform,
      Vector2.zero,
      new Vector2(0f, 1f),
      Vector2.zero,
      Vector2.one
    );
    LayoutOutlineGraphic outline = root.AddComponent<LayoutOutlineGraphic>();
    outline.raycastTarget = false;
    _frames.Add(new TargetFrame(target, outline));
  }

  private void SetRect(RectTransform transform, Rect bounds)
  {
    Vector2 size = bounds.size;
    Vector2 position = new(bounds.x, ViewHeight - bounds.y);
    if (transform.sizeDelta != size)
      transform.sizeDelta = size;
    if (transform.anchoredPosition != position)
      transform.anchoredPosition = position;
  }

  private static LayoutBounds ToBounds(Rect rect) => new(rect.x, rect.y, rect.width, rect.height);

  private static bool CommandModifierHeld() =>
    Input.GetKey(KeyCode.LeftControl)
    || Input.GetKey(KeyCode.RightControl)
    || Input.GetKey(KeyCode.LeftCommand)
    || Input.GetKey(KeyCode.RightCommand);

  private static bool IsGameplayActive() =>
    ADOBase.controller != null
    && !ADOBase.controller.paused
    && ADOBase.conductor != null
    && ADOBase.conductor.isGameWorld;

  private static Rect ToRect(LayoutBounds bounds) => new(bounds.X, bounds.Y, bounds.Width, bounds.Height);

  private sealed class TargetFrame
  {
    public readonly IOverlayLayoutTarget Target;
    public readonly LayoutOutlineGraphic Outline;
    public Rect InitialBounds;

    public TargetFrame(IOverlayLayoutTarget target, LayoutOutlineGraphic outline)
    {
      Target = target;
      Outline = outline;
    }
  }
}
