using System;
using System.Collections.Generic;
using Element.Application.Input;
using Element.Application.KeyViewer;
using Element.Application.Layout;
using Element.Configuration;
using Element.Configuration.Overlay;
using Element.Domain.Layout;
using Element.Presentation.Overlay.Canvas;
using Element.Presentation.OverlayEditor.Canvas;
using Element.Presentation.OverlayEditor.Canvas.AddMenu;
using Element.Presentation.OverlayEditor.Canvas.Confirm;
using Element.Presentation.OverlayEditor.Canvas.ElementBar;
using Element.Presentation.OverlayEditor.Canvas.Layers;
using Element.Presentation.OverlayEditor.Canvas.Toolbar;
using Element.Presentation.OverlayEditor.Kinds;
using Element.Presentation.OverlayEditor.Parts;
using Element.Presentation.Ui.Popovers;
using Element.Presentation.Ui.Theme;
using Element.Presentation.Ui.Widgets;
using ElementUI.Ugui;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using static Element.Presentation.Ui.Theme.UiLocale;
using Object = UnityEngine.Object;

namespace Element.Presentation.OverlayEditor;

/// <summary>
/// The full-screen layout editor: drag, resize, align and delete the
/// overlay's elements and the key viewers, add new ones, and open the editor
/// of what is selected. Its parts live in Canvas/; what differs between
/// kinds of elements is in Kinds/.
/// </summary>
internal sealed class OverlayEditorScreen : ICanvasContext, ICanvasActions, IDisposable
{
  private readonly CanvasSession _session;
  private readonly Action<bool> _onClosed;
  private readonly CanvasCursors _cursors = new();
  private readonly CanvasStatusLine _status = new();
  private readonly CanvasGuides _guides = new();
  private readonly ElementKinds _kinds = new();
  private readonly List<LayoutExclusion> _exclusions = new();
  private readonly CanvasSelection _selection;
  private readonly ToolbarPanel _toolbar;
  private readonly CanvasPopovers _popovers;
  private readonly AddMenuPanel _addMenu;
  private readonly ElementBarPanel _elementBar;
  private readonly CanvasMarqueeInput _marquee;
  private readonly CanvasDragInput _drag;
  private readonly ConfirmPanel _modal;
  private readonly CanvasReset _reset;
  private readonly CanvasKeyboardInput _keyboard;
  private readonly CanvasLayers _layers;
  private readonly LayersPanel _layersPanel;
  private readonly Func<IOverlayLayoutTarget, bool> _edit;
  private LayoutSnapshot _openingLayout;
  private string _openingColor;
  private string _openingFont;
  private GameObject _ownedEvents;
  private bool _previousCursorVisible;
  private CursorLockMode _previousCursorLock;

  public OverlayEditorScreen(
    OverlayCanvasHost host,
    IKeyViewerElements keyViewers,
    TMP_FontAsset font,
    Action<bool> onClosed,
    ElementSettings preferences
  )
  {
    _session = new CanvasSession(host, keyViewers, font, preferences);
    _onClosed = onClosed;
    _selection = new CanvasSelection(_session);
    _toolbar = new ToolbarPanel(_session, _selection, _status, _kinds);
    _popovers = new CanvasPopovers(_session, _toolbar);
    _addMenu = new AddMenuPanel(_session, _toolbar, _popovers.Stack) { Kinds = _kinds };
    _kinds.Add(new TextKind(this, this));
    _kinds.Add(new KeyViewerKind(this, this));
    _kinds.Add(new ShapeKind(this, this));
    _kinds.Add(new ModKind(this, this));
    _elementBar = new ElementBarPanel(_session, _selection, _toolbar, _kinds, _status, this);
    _popovers.Stack.Add(_elementBar, false);
    _marquee = new CanvasMarqueeInput(_session, _selection, _cursors);
    _drag = new CanvasDragInput(_session, _selection, _guides, _cursors, _kinds.SizedInEditor);
    _modal = new ConfirmPanel(_session);
    _reset = new CanvasReset(_session, _modal, this, this, RebuildFrames, _elementBar.MarkStale);
    _edit = _kinds.Edit;
    _keyboard = new CanvasKeyboardInput(_session, _selection, _drag, _toolbar, _kinds, this);
    _layers = new CanvasLayers(_session, _selection, _status);
    _layersPanel = new LayersPanel(_session, _selection, _toolbar, _kinds, _status);
    _popovers.Stack.Add(_layersPanel, false);
    _elementBar.Avoid = () => _layersPanel.Bounds;
  }

  public bool IsOpen => _session.IsOpen;

  // ---- What the parts and kinds read ----

  CanvasSession ICanvasContext.Session => _session;
  CanvasSelection ICanvasContext.Selection => _selection;
  CanvasStatusLine ICanvasContext.Status => _status;
  AddMenuPanel ICanvasContext.AddMenu => _addMenu;

  public void Open()
  {
    if (IsOpen)
      return;
    EnsureRoot();
    _session.SetViewport(Screen.width, Screen.height);
    EnsureEvents();
    RebuildFrames();
    OverlayCanvasHost host = _session.Host;
    _openingLayout = new LayoutSnapshot(host.LayoutSettings);
    _openingColor = _session.Preferences.HighlightColor;
    _openingFont = _session.Preferences.OverlayFont;
    _previousCursorVisible = Cursor.visible;
    _previousCursorLock = Cursor.lockState;
    Cursor.visible = true;
    Cursor.lockState = CursorLockMode.None;
    _session.IsOpen = true;
    host.SetLayoutPreview(true);
    UnityEngine.Canvas.ForceUpdateCanvases();
    host.ApplyLayouts();
    foreach (CanvasFrame frame in _session.Frames)
      frame.Target.SetPreview(true);
    _session.Root.SetActive(true);
    _session.History.Reset(host.LayoutSettings);
    _selection.Clear();
    _drag.Cancel();
    _marquee.End();
    _toolbar.Dragging = false;
    _guides.HideLines();
    _guides.HideSpacing();
    _status.Clear();
    UpdateFrames();
  }

  public void Close(bool save)
  {
    if (!IsOpen)
      return;
    // Closing the whole editor finishes an open full-screen edit the same way.
    IContentEditor editor = _kinds.OpenEditor;
    if (editor != null)
    {
      editor.Close(save, true);
      if (editor.IsOpen)
        return;
    }
    if (save && !_popovers.Commit())
      return;
    if (save && !_toolbar.CommitPendingScale())
      return;
    if (save)
      FinishDrag();
    _session.IsOpen = false;
    _session.Closing = true;
    _drag.Cancel();
    _toolbar.Dragging = false;
    ClosePopovers();
    _elementBar.Close();
    _layers.EndPreview();
    _layers.Peek(null);
    _layersPanel.Close();
    if (!save)
    {
      _openingLayout.Restore(_session.Host.LayoutSettings);
      _session.Preferences.HighlightColor = _openingColor;
      UiTheme.SetHighlight(_openingColor);
      _session.Preferences.OverlayFont = _openingFont;
      _session.Fonts?.SetDefault(_openingFont);
      RefreshLayouts();
    }
    EndPreview();
    _session.Root.SetActive(false);
    _cursors.Set(LayoutHandle.None);
    Cursor.visible = _previousCursorVisible;
    Cursor.lockState = _previousCursorLock;
    InputGate.SetTextEditing(false);
    if (_ownedEvents != null)
    {
      _ownedEvents.SetActive(false);
      UiFactory.Destroy(_ownedEvents);
      _ownedEvents = null;
    }
    _session.Closing = false;
    // The session's undo history ends here, so deleted elements can be released
    // before the settings are saved.
    _session.Host.PruneDisabledInstances();
    _onClosed?.Invoke(save);
  }

  public void Tick()
  {
    if (!IsOpen)
      return;
    OverlayCanvasHost host = _session.Host;
    _guides.HideSpacing();
    EnsureEvents();
    if (_session.ViewportChanged(Screen.width, Screen.height))
      Resize(Screen.width, Screen.height);
    IContentEditor editor = _kinds.OpenEditor;
    if (editor != null)
    {
      InputGate.SetTextEditing(true);
      editor.Tick();
      host.TickLayoutPreview();
      UpdateFrames();
      _cursors.Set(LayoutHandle.None);
      return;
    }
    PopoverStack popovers = _popovers.Stack;
    bool typing = Keyboard.IsTaken;
    bool popoverOpen = popovers.AnyOpen;
    InputGate.SetTextEditing(typing || _modal.IsOpen || popoverOpen);
    UpdatePopupPositions();
    _popovers.Tick();
    if (_modal.IsOpen || popoverOpen)
    {
      if (EditorKeys.Pressed(KeyCode.Escape))
      {
        if (_modal.IsOpen)
          _modal.Close();
        else
          popovers.CloseTop();
      }
      else if (!typing && !_modal.IsOpen && Input.GetMouseButtonDown(0))
      {
        if (!popovers.Contains(Input.mousePosition) && !_toolbar.Bounds.Contains(CanvasSession.Mouse))
          ClosePopovers();
      }
      host.TickLayoutPreview();
      UpdateFrames();
      _cursors.Set(LayoutHandle.None);
      return;
    }
    if (typing)
    {
      host.TickLayoutPreview();
      UpdateFrames();
      return;
    }
    // Esc saves like Save; only Cancel leaves without saving.
    if (EditorKeys.Pressed(KeyCode.Escape))
    {
      Close(true);
      return;
    }
    if (EditorKeys.CommandHeld && EditorKeys.EnterPressed)
    {
      Close(true);
      return;
    }
    if (!UnityEngine.Application.isFocused)
    {
      _marquee.End();
      FinishDrag();
      _toolbar.Dragging = false;
      _guides.HideLines();
      _guides.Refresh(_session.UiScale);
      _cursors.Set(LayoutHandle.None);
      return;
    }
    host.TickLayoutPreview();
    _layersPanel.TrackPointer(Input.mousePosition);
    TrackPointer(CanvasSession.Mouse);
    _keyboard.Handle();
    UpdateFrames();
  }

  // Who the pointer belongs to: a drag in progress, a toolbar, or the canvas.
  private void TrackPointer(Vector2 mouse)
  {
    if (_toolbar.Dragging)
    {
      _toolbar.Move(mouse);
      if (!Input.GetMouseButton(0))
        _toolbar.Dragging = false;
      _cursors.Set(LayoutHandle.Move);
    }
    else if (_elementBar.Dragging)
    {
      _elementBar.Move(mouse);
      if (!Input.GetMouseButton(0))
        _elementBar.Dragging = false;
      _cursors.Set(LayoutHandle.Move);
    }
    else if (_drag.Active)
      _drag.Continue(mouse);
    else if (_marquee.Pending)
      _marquee.Continue(mouse);
    else if (_elementBar.BoundsContain(mouse))
    {
      // Like the main toolbar, the element toolbar drags from anywhere but its controls.
      bool canDrag = !_elementBar.ControlAt(mouse);
      _cursors.Set(canDrag ? LayoutHandle.Move : LayoutHandle.None);
      if (canDrag && Input.GetMouseButtonDown(0))
        _elementBar.BeginDrag(mouse);
    }
    else if (_layersPanel.Contains(Input.mousePosition))
      _cursors.Set(LayoutHandle.None);
    else if (_toolbar.Bounds.Contains(mouse))
    {
      bool canDrag = !_toolbar.ControlAt(mouse);
      _cursors.Set(canDrag ? LayoutHandle.Move : LayoutHandle.None);
      if (canDrag && Input.GetMouseButtonDown(0))
        _toolbar.BeginDrag(mouse);
    }
    else
    {
      int hovered = _drag.FindTarget(mouse, out LayoutHandle handle);
      // With Cmd/Ctrl or Shift, a press toggles or draws a marquee instead of moving.
      bool adding = EditorKeys.CommandHeld || EditorKeys.ShiftHeld;
      _cursors.Set(adding ? LayoutHandle.None : handle);
      if (Input.GetMouseButtonDown(0))
      {
        if (adding || hovered < 0)
          _marquee.Begin(mouse, adding ? hovered : -1, adding);
        else
          PressElement(hovered, handle, mouse);
      }
    }
  }

  private void PressElement(int hovered, LayoutHandle handle, Vector2 mouse)
  {
    if (_drag.Press(hovered, handle, mouse, _edit))
      return;
    _toolbar.ViewModel.DropTypedScale();
    _status.Clear();
  }

  private void FinishDrag() => _drag.Finish();

  internal void Resize(int width, int height)
  {
    bool changed = _session.ViewportChanged(width, height);
    _session.SetViewport(width, height);
    if (changed)
    {
      // Never capture coordinates from a gesture that began in another viewport.
      if (_drag.Active)
      {
        _drag.Cancel();
        RefreshLayouts();
      }
      _guides.HideLines();
      _guides.HideSpacing();
    }
    UpdateFrames();
  }

  /// <summary>In screen pixels from the top left.</summary>
  public Rect ToolbarBounds => _toolbar.Bounds;

  /// <summary>Screen pixels native key viewers must leave uncovered for the editor's own UI.</summary>
  public Rect NativeExclusionBounds
  {
    get
    {
      if (Covered)
        return new Rect(0, 0, _session.ViewWidth, _session.ViewHeight);
      Rect bounds = _toolbar.Bounds;
      PopoverStack popovers = _popovers.Stack;
      for (int i = 0; i < popovers.Count; i++)
      {
        if (!popovers[i].Visible)
          continue;
        Rect popover = PopoverPlacement.ScreenRect(popovers[i].Root);
        bounds = Rect.MinMaxRect(
          Mathf.Min(bounds.xMin, popover.xMin),
          Mathf.Min(bounds.yMin, popover.yMin),
          Mathf.Max(bounds.xMax, popover.xMax),
          Mathf.Max(bounds.yMax, popover.yMax)
        );
      }
      return bounds;
    }
  }

  /// <summary>
  /// Every editor panel with its rounded corners, in screen pixels from the
  /// top left. Native key viewers leave exactly these shapes uncovered.
  /// </summary>
  public IReadOnlyList<LayoutExclusion> NativeExclusions
  {
    get
    {
      _exclusions.Clear();
      if (Covered)
      {
        _exclusions.Add(new LayoutExclusion(new LayoutBounds(0, 0, _session.ViewWidth, _session.ViewHeight), 0));
        return _exclusions;
      }
      AddExclusion(_toolbar.Bounds, _session.ToolbarScale);
      PopoverStack popovers = _popovers.Stack;
      for (int i = 0; i < popovers.Count; i++)
        if (popovers[i].Visible)
          AddExclusion(PopoverPlacement.ScreenRect(popovers[i].Root), popovers[i].Root.localScale.x);
      LayoutExclusions.Merge(_exclusions);
      return _exclusions;
    }
  }

  // Panels use the shared 8-unit rounded corner, at their own scale.
  private void AddExclusion(Rect bounds, float scale) =>
    _exclusions.Add(new LayoutExclusion(new LayoutBounds(bounds.x, bounds.y, bounds.width, bounds.height), 8 * scale));

  // A dialog or a full-screen editor covers the whole view.
  private bool Covered => _modal.IsOpen || _kinds.OpenEditor != null;

  private void UpdateFrames()
  {
    bool editingContent = _kinds.OpenEditor != null;
    bool covered = _modal.IsOpen || editingContent;
    _selection.Normalize();
    // A full-screen editor stands alone; the toolbar would show through its dim.
    _toolbar.Show(!editingContent);
    List<CanvasFrame> frames = _session.Frames;
    HashSet<string> peeked = _layers.Peeked;
    for (int index = 0; index < frames.Count; index++)
    {
      CanvasFrame frame = frames[index];
      bool selected = !covered && _selection.Contains(frame.Target);
      // The pointer over a layer card outlines its elements in the highlight color.
      bool peek = !covered && !selected && peeked.Contains(frame.Target.Id);
      (frame.Target as IExternalLayoutOutline)?.SetSelected(selected || peek);
      bool visible =
        (selected || peek) && !(frame.Target is IExternalLayoutOutline external && external.OwnsSelectionOutline);
      frame.Outline.gameObject.SetActive(visible);
      if (!visible)
        continue;
      frame.Outline.color = peek ? UiPalette.Accent : Color.white;
      Rect bounds = frame.Target.Bounds;
      _session.SetRect(frame.Outline.rectTransform, bounds);
      frame.Outline.SetScale(_session.UiScale);
      frame.Outline.SetHandles(_drag.Resizable(frame.Target));
      Rect panel = NativeExclusionBounds;
      frame.Outline.SetExclusion(new Rect(panel.x - bounds.x, -panel.yMax + bounds.y, panel.width, panel.height));
    }
    _elementBar.Sync(covered, (_drag.Active && _drag.Moved) || _popovers.Stack.AnyOpen);
    _layersPanel.Sync(covered);
    _toolbar.ViewModel.LayersOpen.Value = _layersPanel.Visible;
    _toolbar.Refresh();
    _guides.Refresh(_session.UiScale);
    RaisePopovers();
  }

  private void RaisePopovers()
  {
    _popovers.Stack.Raise();
    _elementBar.Raise();
    _modal.Raise();
    _kinds.OpenEditor?.Raise();
  }

  private void UpdatePopupPositions()
  {
    _popovers.Place();
    _addMenu.Place();
    _layersPanel.Place();
    _elementBar.Place();
    _modal.Follow();
  }

  private void RebuildFrames()
  {
    _session.RebuildFrames();
    _toolbar.Root.SetAsLastSibling();
  }

  private void EndPreview()
  {
    foreach (CanvasFrame frame in _session.Frames)
    {
      (frame.Target as IExternalLayoutOutline)?.SetSelected(false);
      frame.Target.SetPreview(false);
    }
    _session.Host.SetLayoutPreview(false);
  }

  private void EnsureEvents()
  {
    if (EventSystem.current != null)
      return;
    _ownedEvents = new GameObject("Element Layout Events", typeof(EventSystem), typeof(StandaloneInputModule));
    if (UnityEngine.Application.isPlaying)
      Object.DontDestroyOnLoad(_ownedEvents);
  }

  private void EnsureRoot()
  {
    if (_session.Root != null)
      return;
    _session.Ui = _session.NewUi();
    GameObject root = new("Element Layout Editor", typeof(RectTransform));
    _session.Root = root;
    if (UnityEngine.Application.isPlaying)
      Object.DontDestroyOnLoad(root);
    UnityEngine.Canvas canvas = root.AddComponent<UnityEngine.Canvas>();
    canvas.renderMode = RenderMode.ScreenSpaceOverlay;
    canvas.sortingOrder = 32760;
    root.AddComponent<GraphicRaycaster>();
    Image blocker = _session.Ui.Panel("Block game clicks", root.transform, 0, 0, 0, 0, UnityEngine.Color.clear, false);
    UiFactory.Stretch(blocker.rectTransform, 0, 0, 0, 0);
    blocker.raycastTarget = true;
    _guides.Build(_session.Ui, root.transform);
    _toolbar.Build(this);
    _marquee.Toolbar = _toolbar.Root;
    root.SetActive(false);
  }

  private void RefreshLayouts()
  {
    _toolbar.ViewModel.ForgetScale();
    _status.Clear();
    _session.Host.ApplyLayouts();
    if (_session.KeyViewers != null)
      foreach (IOverlayLayoutTarget target in _session.KeyViewers.Targets)
        target.RefreshLayout();
    _selection.Prune();
  }

  private void Undo()
  {
    FinishDrag();
    _elementBar.MarkStale();
    _session.History.Undo(_session.Host.LayoutSettings);
    RefreshLayouts();
  }

  private void Redo()
  {
    FinishDrag();
    _elementBar.MarkStale();
    _session.History.Redo(_session.Host.LayoutSettings);
    RefreshLayouts();
  }

  private void EditSelected()
  {
    if (_selection.HasSelection)
      _kinds.Edit(_selection.Primary);
  }

  private void ResetSelected()
  {
    if (!_selection.HasSelection)
      return;
    ClosePopovers();
    foreach (IOverlayLayoutTarget target in _selection.Targets)
      target.ResetLayout();
    _toolbar.ViewModel.ForgetScale();
    _status.Clear();
    _session.Commit();
  }

  private void DeleteSelected()
  {
    if (!_selection.HasSelection)
      return;
    ClosePopovers();
    // Every selected element goes, as one undo step.
    foreach (IOverlayLayoutTarget target in _selection.Targets)
    {
      (target as IExternalLayoutOutline)?.SetSelected(false);
      target.SetEnabled(false);
    }
    _selection.SelectOnly(-1);
    _drag.Cancel();
    // A group left with one element is no group any more.
    _layers.Changed();
    RefreshLayouts();
    UpdateFrames();
  }

  // A new element starts in the middle of the view, selected, as one undo step.
  private void PlaceNewElement(IOverlayLayoutTarget target, bool commit)
  {
    UnityEngine.Canvas.ForceUpdateCanvases();
    Rect bounds = target.Bounds;
    if (bounds.width > 0 && bounds.height > 0)
    {
      bounds.center = new Vector2(_session.ViewWidth * .5f, _session.ViewHeight * .5f);
      target.SetBounds(bounds);
      target.CommitLayout(true, initialize: true);
    }
    if (_session.IndexOf(target) < 0)
      _session.AddFrame(target);
    _selection.Selected = _session.IndexOf(target);
    if (commit)
      _session.Commit();
    _toolbar.Root.SetAsLastSibling();
    UpdateFrames();
  }

  // ---- Popovers above the toolbar ----

  private void ClosePopovers() => _popovers.Stack.CloseAll();

  private bool Settle()
  {
    if (!_popovers.Commit())
      return false;
    ClosePopovers();
    FinishDrag();
    _toolbar.Dragging = false;
    return true;
  }

  private void ToggleAddMenu()
  {
    if (_addMenu.IsOpen)
    {
      _addMenu.Close();
      return;
    }
    if (!Settle())
      return;
    Selectable first = _addMenu.Open();
    UpdatePopupPositions();
    if (first != null)
      EventSystem.current?.SetSelectedGameObject(first.gameObject);
  }

  private void ToggleColor()
  {
    HighlightColorPanel color = _popovers.Color;
    if (color.Visible)
      color.Hide();
    else
    {
      if (!_popovers.Commit())
        return;
      ClosePopovers();
      color.Show();
      _popovers.Stack.Opened(color);
      UpdatePopupPositions();
    }
    FinishDrag();
    _toolbar.Dragging = false;
  }

  // The default font of every text; a text's own fonts stay as they are.
  private void ToggleFont()
  {
    if (_session.Fonts == null)
      return;
    UiFontPopover font = _popovers.Font(PickDefaultFont);
    if (font.Visible)
      font.Hide();
    else
    {
      if (!_popovers.Commit())
        return;
      ClosePopovers();
      font.Show(
        L("오버레이 기본 폰트", "Overlay default font"),
        _session.Preferences.OverlayFont ?? "",
        L("메이플스토리", "MapleStory"),
        L("기본 제공", "Built-in")
      );
      _popovers.Stack.Opened(font);
      UpdatePopupPositions();
    }
    FinishDrag();
    _toolbar.Dragging = false;
  }

  private void PickDefaultFont(string key)
  {
    _session.Preferences.OverlayFont = OverlayFontKey.Normalize(key);
    _session.Fonts?.SetDefault(_session.Preferences.OverlayFont);
  }

  // ---- Layers and groups ----

  // The layers panel stays open beside the canvas until its button closes it.
  private void ToggleLayers()
  {
    FinishDrag();
    _toolbar.Dragging = false;
    if (_layersPanel.Visible)
      _layersPanel.Close();
    else
    {
      _layersPanel.Open(this);
      UpdatePopupPositions();
    }
    UpdateFrames();
  }

  // After a change to the order or the groups: the element toolbar and the
  // outlines follow.
  private void AfterLayers(bool changed)
  {
    if (!changed)
      return;
    _elementBar.MarkStale();
    _toolbar.ViewModel.ForgetScale();
    UpdateFrames();
  }

  // ---- Everything the editor is asked to do: the player's commands, then what its parts ask ----

  void ICanvasActions.Undo() => Undo();

  void ICanvasActions.Redo() => Redo();

  void ICanvasActions.Cancel() => Close(false);

  void ICanvasActions.Save() => Close(true);

  void ICanvasActions.ResetAll() => _reset.Confirm();

  void ICanvasActions.ResetSelected() => ResetSelected();

  void ICanvasActions.Delete() => DeleteSelected();

  void ICanvasActions.Add() => ToggleAddMenu();

  void ICanvasActions.Color() => ToggleColor();

  void ICanvasActions.Font() => ToggleFont();

  void ICanvasActions.Edit() => EditSelected();

  void ICanvasActions.Layers() => ToggleLayers();

  void ICanvasActions.Group() => AfterLayers(_layers.Group());

  void ICanvasActions.Ungroup() => AfterLayers(_layers.Ungroup());

  void ICanvasActions.Arrange(bool forward, bool toEnd) => AfterLayers(_layers.Arrange(forward, toEnd));

  void ICanvasActions.MoveLayers(IReadOnlyCollection<string> ids, string behindId, string group)
  {
    _layers.Move(ids, behindId, group);
    AfterLayers(true);
  }

  void ICanvasActions.RenameGroup(string group, string name)
  {
    _layers.Rename(group, name);
    AfterLayers(true);
  }

  void ICanvasActions.SelectLayer(string id, string group, bool adding)
  {
    FinishDrag();
    _layers.Select(id, group, adding);
    _toolbar.ViewModel.DropTypedScale();
    UpdateFrames();
  }

  void ICanvasActions.PeekLayers(IReadOnlyCollection<string> ids) => _layers.Peek(ids);

  void ICanvasActions.PreviewLayers(IReadOnlyCollection<string> ids, string behindId) => _layers.Preview(ids, behindId);

  bool ICanvasActions.Settle() => Settle();

  void ICanvasActions.PlaceNew(IOverlayLayoutTarget target, bool commit) => PlaceNewElement(target, commit);

  void ICanvasActions.RefreshLayouts() => RefreshLayouts();

  void ICanvasActions.UpdateFrames() => UpdateFrames();

  public void Dispose()
  {
    _session.Closing = true;
    // Disposal must not reopen settings through the normal close callback.
    // Torn down while open (the mod turned off), it keeps what was edited,
    // an open full-screen edit included; only Cancel throws edits away.
    _kinds.Dispose();
    if (IsOpen)
    {
      FinishDrag();
      RefreshLayouts();
      EndPreview();
      Cursor.visible = _previousCursorVisible;
      Cursor.lockState = _previousCursorLock;
    }
    _session.IsOpen = false;
    _cursors.Dispose();
    ClosePopovers();
    _elementBar.Close();
    _layers.EndPreview();
    _layersPanel.Close();
    _modal.Close();
    _toolbar.Dispose();
    _popovers.Dispose();
    _session.Ui?.Dispose();
    InputGate.SetTextEditing(false);
    if (_ownedEvents != null)
      UiFactory.Destroy(_ownedEvents);
    if (_session.Root != null)
      UiFactory.Destroy(_session.Root);
    _session.Root = null;
  }
}
