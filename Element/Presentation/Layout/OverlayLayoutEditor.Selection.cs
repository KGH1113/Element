using System.Collections.Generic;
using Element.Domain.Layout;
using Element.Presentation.Settings;
using UnityEngine;
using UnityEngine.UI;
using static Element.Presentation.Settings.SettingsLocale;

namespace Element.Presentation.Layout;

// Several elements at once: Cmd/Ctrl or Shift + click adds or removes one;
// dragging over empty space, or with Cmd/Ctrl or Shift anywhere, selects
// what lies fully inside. Dragging any selected element moves them all.
//
// _selected stays the primary element the toolbars follow. Code that sets it
// to an element outside the selection selects that element alone, and -1
// clears the selection; NormalizeSelection keeps the two in step.
internal sealed partial class OverlayLayoutEditor
{
  private readonly List<IOverlayLayoutTarget> _selection = new();
  private readonly List<IOverlayLayoutTarget> _marqueeBase = new();
  private readonly List<Rect> _groupStart = new();
  private readonly List<IOverlayLayoutTarget> _groupTargets = new();
  private readonly List<LayoutBounds> _groupBounds = new();

  // A press on one of several selected elements: if it doesn't move, that
  // element alone stays selected.
  private int _collapseTo = -1;

  // A press on empty space, or with a selection modifier: a marquee once it moves.
  private bool _marqueePending,
    _marqueeActive,
    _marqueeAdds;
  private int _marqueeTarget = -1;
  private Vector2 _marqueeStart;
  private Image _marqueeFill;
  private LayoutOutlineGraphic _marqueeOutline;

  private int SelectedCount => _selection.Count;

  private bool MultiSelected => _selection.Count > 1;

  private bool IsSelected(IOverlayLayoutTarget target) => _selection.Contains(target);

  private static bool Selectable(IOverlayLayoutTarget target) => target.Available && target.Enabled;

  private static bool SelectionModifierHeld() =>
    CommandModifierHeld() || Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

  // Drops what can no longer be selected and follows assignments to _selected.
  private void NormalizeSelection()
  {
    for (int i = _selection.Count - 1; i >= 0; i--)
      if (!Selectable(_selection[i]) || IndexOf(_selection[i]) < 0)
        _selection.RemoveAt(i);
    if (_selected < 0 || _selected >= _frames.Count || !Selectable(_frames[_selected].Target))
    {
      _selected = -1;
      _selection.Clear();
      return;
    }
    IOverlayLayoutTarget primary = _frames[_selected].Target;
    if (!_selection.Contains(primary))
    {
      _selection.Clear();
      _selection.Add(primary);
    }
  }

  // After undo: keep whatever is still there, even when the primary is gone.
  private void PruneSelection()
  {
    for (int i = _selection.Count - 1; i >= 0; i--)
      if (!Selectable(_selection[i]) || IndexOf(_selection[i]) < 0)
        _selection.RemoveAt(i);
    // A primary that is still there keeps the selection, or was just assigned.
    if (!HasSelection)
      _selected = _selection.Count > 0 ? IndexOf(_selection[_selection.Count - 1]) : -1;
    NormalizeSelection();
  }

  private void SelectOnly(int index)
  {
    _selection.Clear();
    _selected = index >= 0 && index < _frames.Count && Selectable(_frames[index].Target) ? index : -1;
    if (_selected >= 0)
      _selection.Add(_frames[_selected].Target);
  }

  // Cmd/Ctrl or Shift + click: in if it was out, out if it was in.
  private void ToggleSelection(int index)
  {
    if (index < 0 || index >= _frames.Count || !Selectable(_frames[index].Target))
      return;
    IOverlayLayoutTarget target = _frames[index].Target;
    if (_selection.Remove(target))
    {
      if (_selected == index)
        _selected = _selection.Count > 0 ? IndexOf(_selection[_selection.Count - 1]) : -1;
      return;
    }
    _selection.Add(target);
    _selected = index;
  }

  private void SelectAll()
  {
    _selection.Clear();
    _selected = -1;
    for (int i = 0; i < _frames.Count; i++)
      if (Selectable(_frames[i].Target))
      {
        _selection.Add(_frames[i].Target);
        _selected = i;
      }
  }

  // ---- Marquee ----

  private void BeginMarquee(Vector2 mouse, int target, bool adds)
  {
    _marqueePending = true;
    _marqueeActive = false;
    _marqueeAdds = adds;
    _marqueeTarget = target;
    _marqueeStart = mouse;
    _marqueeBase.Clear();
    if (adds)
      _marqueeBase.AddRange(_selection);
  }

  private void ContinueMarquee(Vector2 mouse)
  {
    _cursors.Set(LayoutHandle.None);
    if (!_marqueeActive && (mouse - _marqueeStart).sqrMagnitude > 9)
      _marqueeActive = true;
    if (_marqueeActive)
      SelectInside(mouse);
    if (Input.GetMouseButton(0))
      DrawMarquee(mouse);
    else
      ReleaseMarquee();
  }

  // Released without moving: a modifier click toggles what it hit, a plain
  // click on empty space clears the selection.
  private void ReleaseMarquee()
  {
    if (!_marqueeActive)
    {
      if (_marqueeTarget >= 0)
        ToggleSelection(_marqueeTarget);
      else if (!_marqueeAdds)
        SelectOnly(-1);
    }
    EndMarquee();
  }

  private void EndMarquee()
  {
    _marqueePending = _marqueeActive = false;
    _marqueeTarget = -1;
    _marqueeBase.Clear();
    if (_marqueeFill != null && _marqueeFill.gameObject.activeSelf)
      _marqueeFill.gameObject.SetActive(false);
  }

  // Elements whose visible content lies fully inside the marquee join the
  // selection the press started with.
  private void SelectInside(Vector2 mouse)
  {
    Rect area = Rect.MinMaxRect(
      Mathf.Min(_marqueeStart.x, mouse.x),
      Mathf.Min(_marqueeStart.y, mouse.y),
      Mathf.Max(_marqueeStart.x, mouse.x),
      Mathf.Max(_marqueeStart.y, mouse.y)
    );
    _selection.Clear();
    _selection.AddRange(_marqueeBase);
    int primary = _selection.Count > 0 ? IndexOf(_selection[_selection.Count - 1]) : -1;
    for (int i = 0; i < _frames.Count; i++)
    {
      IOverlayLayoutTarget target = _frames[i].Target;
      if (!Selectable(target) || _selection.Contains(target))
        continue;
      Rect content = target.SelectionBounds;
      if (content.width <= 0 || content.height <= 0)
        continue;
      // Native viewers measure from their own window origin.
      Vector2 offset = PointerFor(target, mouse) - mouse;
      Rect inSpace = new(area.position + offset, area.size);
      if (
        content.xMin >= inSpace.xMin
        && content.xMax <= inSpace.xMax
        && content.yMin >= inSpace.yMin
        && content.yMax <= inSpace.yMax
      )
      {
        _selection.Add(target);
        primary = i;
      }
    }
    _selected = primary;
  }

  private void DrawMarquee(Vector2 mouse)
  {
    if (!_marqueeActive)
      return;
    if (_marqueeFill == null)
    {
      _marqueeFill = _ui.Panel("Selection area", _root.transform, 0, 0, 10, 10, Color.white, false);
      _marqueeFill.raycastTarget = false;
      _marqueeFill.rectTransform.anchorMin = _marqueeFill.rectTransform.anchorMax = Vector2.zero;
      _marqueeFill.rectTransform.pivot = new Vector2(0, 1);
      _marqueeOutline = _ui.Rect("Selection area outline", _marqueeFill.transform, 0, 0, 10, 10)
        .gameObject.AddComponent<LayoutOutlineGraphic>();
      _marqueeOutline.raycastTarget = false;
      SettingsUiFactory.Stretch(_marqueeOutline.rectTransform, 0, 0, 0, 0);
    }
    Color accent = SettingsPalette.SoftAccent;
    _marqueeFill.color = new Color(accent.r, accent.g, accent.b, .06f);
    _marqueeOutline.color = accent;
    _marqueeOutline.SetScale(UiScale * .6f);
    Rect area = Rect.MinMaxRect(
      Mathf.Min(_marqueeStart.x, mouse.x),
      Mathf.Min(_marqueeStart.y, mouse.y),
      Mathf.Max(_marqueeStart.x, mouse.x),
      Mathf.Max(_marqueeStart.y, mouse.y)
    );
    if (!_marqueeFill.gameObject.activeSelf)
      _marqueeFill.gameObject.SetActive(true);
    SetRect(_marqueeFill.rectTransform, area);
    // Under the toolbars, over the elements.
    _marqueeFill.transform.SetSiblingIndex(Mathf.Max(0, _toolbar.GetSiblingIndex() - 1));
  }

  // ---- Moving several at once ----

  private void BeginGroupDrag()
  {
    _groupTargets.Clear();
    _groupStart.Clear();
    foreach (IOverlayLayoutTarget target in _selection)
    {
      _groupTargets.Add(target);
      _groupStart.Add(target.Bounds);
    }
  }

  private bool GroupDragging => _groupTargets.Count > 1;

  private void DragGroup(Vector2 mouse)
  {
    IOverlayLayoutTarget primary = _frames[_dragging].Target;
    Vector2 pointer = PointerFor(primary, mouse);
    float dx = pointer.x - _pointerStart.x,
      dy = pointer.y - _pointerStart.y;
    ClampGroupMove(_groupStart, ref dx, ref dy);
    if (_preferences.LayoutSnapping && !Input.GetKey(KeyCode.LeftAlt) && !Input.GetKey(KeyCode.RightAlt))
    {
      _groupBounds.Clear();
      foreach (Rect start in _groupStart)
        _groupBounds.Add(new LayoutBounds(start.x + dx, start.y + dy, start.width, start.height));
      LayoutBounds group = LayoutArrange.Union(_groupBounds);
      _snapReferences.Clear();
      for (int i = 0; i < _frames.Count; i++)
        if (Selectable(_frames[i].Target) && !_groupTargets.Contains(_frames[i].Target))
          _snapReferences.Add(ToBounds(_frames[i].Target.Bounds));
      LayoutBounds snapped = LayoutSnap.Move(
        group,
        _snapReferences,
        Screen.width,
        Screen.height,
        7 * UiScale,
        out _guideX,
        out _guideY,
        out _horizontalSpacing,
        out _verticalSpacing
      );
      dx += snapped.X - group.X;
      dy += snapped.Y - group.Y;
      ClampGroupMove(_groupStart, ref dx, ref dy);
    }
    else
      _guideX = _guideY = float.NaN;
    for (int i = 0; i < _groupTargets.Count; i++)
    {
      Rect start = _groupStart[i];
      _groupTargets[i].SetBounds(new Rect(start.x + dx, start.y + dy, start.width, start.height));
    }
  }

  // The most any element of the group may move while each stays on screen.
  private static void ClampGroupMove(List<Rect> starts, ref float dx, ref float dy)
  {
    float x = dx,
      y = dy;
    foreach (Rect start in starts)
    {
      LayoutBounds moved = OverlayLayoutGeometry.Drag(
        ToBounds(start),
        LayoutHandle.Move,
        dx,
        dy,
        Screen.width,
        Screen.height
      );
      float allowedX = moved.X - start.x,
        allowedY = moved.Y - start.y;
      x = dx >= 0 ? Mathf.Min(x, allowedX) : Mathf.Max(x, allowedX);
      y = dy >= 0 ? Mathf.Min(y, allowedY) : Mathf.Max(y, allowedY);
    }
    dx = x;
    dy = y;
  }

  // Arrow keys move every selected element by the same step.
  private void NudgeSelection(float dx, float dy)
  {
    _groupStart.Clear();
    foreach (IOverlayLayoutTarget target in _selection)
      _groupStart.Add(target.Bounds);
    ClampGroupMove(_groupStart, ref dx, ref dy);
    for (int i = 0; i < _selection.Count; i++)
    {
      Rect start = _groupStart[i];
      _selection[i].SetBounds(new Rect(start.x + dx, start.y + dy, start.width, start.height));
      _selection[i].CommitLayout(true);
    }
    _groupStart.Clear();
  }

  private string SelectionLabel() =>
    MultiSelected
      ? L(_selection.Count + "개 선택됨", _selection.Count + " selected")
      : ElementLabel(_frames[_selected].Target);
}
