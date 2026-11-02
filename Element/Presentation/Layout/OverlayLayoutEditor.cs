using System;
using System.Collections.Generic;
using Element.Configuration;
using Element.Domain.Layout;
using Element.Presentation.Overlay.Elements;
using Element.Presentation.Overlay.Shared;
using Element.Presentation.Settings;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Element.Presentation.Layout;

internal sealed partial class OverlayLayoutEditor : IDisposable
{
  private const float MaximumScale = 10f;
  private readonly OverlayCanvasHost _host;
  private readonly IKeyViewerElements _keyViewers;
  private readonly List<TargetFrame> _frames = new();
  private LayoutSnapshot _openingLayout;
  private string _openingColor;
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
  private Vector2 _pressPosition;
  private bool _pressMoved;
  private float _dragScale;
  private float _lastClickTime = -1;
  private int _lastClickTarget = -1;
  private bool _previousCursorVisible;
  private CursorLockMode _previousCursorLock;
  private int _themeVersion = -1;

  public bool IsOpen { get; private set; }

  public OverlayLayoutEditor(
    OverlayCanvasHost host,
    IKeyViewerElements keyViewers,
    TMP_FontAsset font,
    Action<bool> onClosed,
    ElementSettings preferences
  )
  {
    _host = host;
    _keyViewers = keyViewers;
    _font = font;
    _onClosed = onClosed;
    _preferences = preferences;
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
      frame.Target.SetPreview(true);
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
    // Closing the whole editor finishes an open text or shape edit the same way.
    if (_textEditor != null && _textEditor.IsOpen)
    {
      _textEditor.Close(save, true);
      if (_textEditor.IsOpen)
        return;
    }
    if (_shapeEditor != null && _shapeEditor.IsOpen)
    {
      _shapeEditor.Close(save, true);
      if (_shapeEditor.IsOpen)
        return;
    }
    if (save && !CommitPopovers())
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
    ClosePopovers();
    CloseElementSettings();
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
    // The session's undo history ends here, so deleted elements can be released
    // before the settings are saved.
    _host.PruneDisabledInstances();
    _onClosed?.Invoke(save);
  }

  public void Tick()
  {
    if (!IsOpen)
      return;
    _horizontalSpacing = _verticalSpacing = default;
    EnsureEvents();
    if (_viewportWidth != Screen.width || _viewportHeight != Screen.height)
      Resize(Screen.width, Screen.height);
    if (_textEditor != null && _textEditor.IsOpen)
    {
      UIInteractionState.SetTextEditing(true);
      _textEditor.Tick();
      _host.TickLayoutPreview();
      UpdateFrames();
      _cursors.Set(LayoutHandle.None);
      return;
    }
    if (_shapeEditor != null && _shapeEditor.IsOpen)
    {
      UIInteractionState.SetTextEditing(true);
      _shapeEditor.Tick();
      _host.TickLayoutPreview();
      UpdateFrames();
      _cursors.Set(LayoutHandle.None);
      return;
    }
    bool typing = (_scaleInput != null && _scaleInput.isFocused) || PopoverTyping;
    UIInteractionState.SetTextEditing(typing || _modalRoot != null || AnyPopoverVisible);
    UpdatePopupPositions();
    if (_modalRoot != null || AnyPopoverVisible)
    {
      if (Input.GetKeyDown(KeyCode.Escape))
      {
        if (_modalRoot != null)
          CloseModal();
        else
          CloseTopPopover();
      }
      else if (!typing && _modalRoot == null && Input.GetMouseButtonDown(0))
      {
        Vector2 p = new(Input.mousePosition.x, Screen.height - Input.mousePosition.y);
        if (!PopoverContains(p) && !ToolbarBounds.Contains(p))
          ClosePopovers();
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
    else if (_elementBarDragging)
    {
      MoveElementBar(mouse);
      if (!Input.GetMouseButton(0))
        _elementBarDragging = false;
      _cursors.Set(LayoutHandle.Move);
    }
    else if (_dragging >= 0)
      ContinueDrag(mouse);
    else if (SettingsPanelContains(mouse))
    {
      // Like the main toolbar, the element toolbar drags from anywhere but its controls.
      bool canDrag = !ElementBarControlAt(mouse);
      _cursors.Set(canDrag ? LayoutHandle.Move : LayoutHandle.None);
      if (canDrag && Input.GetMouseButtonDown(0))
        BeginElementBarDrag(mouse);
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
        bool doubleClick =
          hovered == _lastClickTarget && hovered == _selected && Time.unscaledTime - _lastClickTime < .35f;
        _lastClickTarget = hovered;
        _lastClickTime = Time.unscaledTime;
        if (doubleClick && OpenContentEditor(_frames[hovered].Target))
          return;
        _scalePending = false;
        _scaleErrorUntil = 0;
        _selected = _dragging = hovered;
        _dragHandle = handle;
        _dragStart = _frames[hovered].Target.Bounds;
        _pointerStart = PointerFor(_frames[hovered].Target, mouse);
        _pressPosition = mouse;
        _pressMoved = false;
        _dragScale = ValidScale(_frames[hovered].Target.EffectiveScale);
      }
      else if (Input.GetMouseButtonDown(0))
        _selected = -1;
    }
    HandleKeyboard();
    UpdateFrames();
  }

  private void ContinueDrag(Vector2 mouse)
  {
    TargetFrame frame = _frames[_dragging];
    if ((mouse - _pressPosition).sqrMagnitude > 9)
      _pressMoved = true;
    if (frame.Target.Available && Input.GetMouseButton(0) && _pressMoved)
      DragBox(frame, mouse);
    _cursors.Set(_dragHandle);
    if (!Input.GetMouseButton(0))
    {
      FinishDrag();
      _guideX = _guideY = float.NaN;
    }
  }

  private void DragBox(TargetFrame frame, Vector2 mouse)
  {
    Vector2 pointer = PointerFor(frame.Target, mouse);
    float dx = pointer.x - _pointerStart.x,
      dy = pointer.y - _pointerStart.y;
    LayoutBounds resized = OverlayLayoutGeometry.Drag(
      ToBounds(_dragStart),
      _dragHandle,
      dx,
      dy,
      Screen.width,
      Screen.height,
      Mathf.Min(1f, 0.2f / _dragScale),
      Mathf.Max(1f, MaximumScale / _dragScale)
    );
    if (
      _dragHandle == LayoutHandle.Move
      && _preferences.LayoutSnapping
      && !Input.GetKey(KeyCode.LeftAlt)
      && !Input.GetKey(KeyCode.RightAlt)
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
    ClosePopovers();
    CloseElementSettings();
    CloseModal();
    _colorPopover?.Dispose();
    _textEditor?.Dispose();
    _shapeEditor?.Dispose();
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
    if (
      !IsGameplayActive()
      && (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
      && OpenContentEditor(_frames[_selected].Target)
    )
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
    // Texts and shapes are sized in their editor, so + and − only scale the rest.
    bool scalable = !SizedInEditor(_frames[_selected].Target);
    bool grow =
      scalable
      && (Input.GetKeyDown(KeyCode.Equals) || Input.GetKeyDown(KeyCode.Plus) || Input.GetKeyDown(KeyCode.KeypadPlus));
    bool shrink = scalable && (Input.GetKeyDown(KeyCode.Minus) || Input.GetKeyDown(KeyCode.KeypadMinus));
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
    IOverlayLayoutTarget target = _frames[_dragging].Target;
    target.CommitLayout(_dragHandle == LayoutHandle.Move);
    _dragging = -1;
    _history.Commit(_host.LayoutSettings);
  }

  private int FindTarget(Vector2 pointer, out LayoutHandle bestHandle)
  {
    // The selected rectangle owns its editing area, including where another
    // element's compact content overlaps it. New copies must remain draggable.
    if (HasSelection)
    {
      IOverlayLayoutTarget selected = _frames[_selected].Target;
      Vector2 selectedPointer = PointerFor(selected, pointer);
      bestHandle = OverlayLayoutGeometry.HitTest(
        ToBounds(selected.Bounds),
        selectedPointer.x,
        selectedPointer.y,
        SizedInEditor(selected) ? 0 : 10 * UiScale
      );
      // Texts and shapes only move; their size is set in their editor.
      if (bestHandle != LayoutHandle.None && SizedInEditor(selected))
        bestHandle = LayoutHandle.Move;
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
      bool resizable = index == _selected && !SizedInEditor(target);
      LayoutHandle handle = OverlayLayoutGeometry.HitTest(
        ToBounds(bounds),
        localPointer.x,
        localPointer.y,
        resizable ? 10 * UiScale : 0
      );
      if (!resizable && handle != LayoutHandle.None)
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
    bool editingContent = ContentEditorOpen;
    bool covered = _modalRoot != null || editingContent;
    // The text and shape editors stand alone; the toolbar would show through their dim.
    if (_toolbar.gameObject.activeSelf == editingContent)
      _toolbar.gameObject.SetActive(!editingContent);
    for (int index = 0; index < _frames.Count; index++)
    {
      TargetFrame frame = _frames[index];
      bool selected = frame.Target.Available && frame.Target.Enabled && index == _selected && !covered;
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
    SyncElementSettings();
    UpdateToolbar();
    UpdateGuides();
    RaisePopovers();
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
    if (_keyViewers != null)
      foreach (IOverlayLayoutTarget target in _keyViewers.Targets)
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

  private int IndexOf(IOverlayLayoutTarget target)
  {
    for (int i = 0; i < _frames.Count; i++)
      if (_frames[i].Target == target)
        return i;
    return -1;
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

    public TargetFrame(IOverlayLayoutTarget target, LayoutOutlineGraphic outline)
    {
      Target = target;
      Outline = outline;
    }
  }
}
