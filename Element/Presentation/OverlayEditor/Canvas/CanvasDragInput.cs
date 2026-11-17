using System;
using System.Collections.Generic;
using Element.Application.Layout;
using Element.Domain.Layout;
using Element.Presentation.OverlayEditor.Parts;
using UnityEngine;

namespace Element.Presentation.OverlayEditor.Canvas;

/// <summary>
/// Pressing, dragging and resizing elements on the canvas. Pressing one of
/// several selected elements moves them all; a press that doesn't move
/// selects it alone. Moves snap to other elements unless Alt is held.
/// </summary>
internal sealed class CanvasDragInput
{
  private readonly CanvasSession _session;
  private readonly CanvasSelection _selection;
  private readonly CanvasGuides _guides;
  private readonly CanvasCursors _cursors;
  private readonly Func<IOverlayLayoutTarget, bool> _sizedInEditor;
  private readonly List<LayoutBounds> _snapReferences = new();
  private readonly List<Rect> _groupStart = new();
  private readonly List<IOverlayLayoutTarget> _groupTargets = new();
  private readonly List<LayoutBounds> _groupBounds = new();
  private int _dragging = -1;
  private LayoutHandle _handle;
  private Rect _start;
  private Vector2 _pointerStart;
  private Vector2 _pressPosition;
  private bool _pressMoved;
  private float _scale;
  private float _lastClickTime = -1;
  private int _lastClickTarget = -1;

  // A press on one of several selected elements: if it doesn't move, that
  // element alone stays selected.
  private int _collapseTo = -1;

  /// <param name="sizedInEditor">Whether an element is sized in its editor: it only moves.</param>
  public CanvasDragInput(
    CanvasSession session,
    CanvasSelection selection,
    CanvasGuides guides,
    CanvasCursors cursors,
    Func<IOverlayLayoutTarget, bool> sizedInEditor
  )
  {
    _session = session;
    _selection = selection;
    _guides = guides;
    _cursors = cursors;
    _sizedInEditor = sizedInEditor;
  }

  /// <summary>The frame index being dragged, or -1.</summary>
  public int Dragging => _dragging;

  public bool Active => _dragging >= 0;

  /// <summary>Whether the press has moved far enough to drag.</summary>
  public bool Moved => _pressMoved;

  /// <summary>
  /// A press on <paramref name="hovered"/>. A second press on the selected
  /// element opens its editor through <paramref name="edit"/>; true when it did.
  /// </summary>
  public bool Press(int hovered, LayoutHandle handle, Vector2 mouse, Func<IOverlayLayoutTarget, bool> edit)
  {
    bool doubleClick =
      hovered == _lastClickTarget && hovered == _selection.Selected && Time.unscaledTime - _lastClickTime < .35f;
    _lastClickTarget = hovered;
    _lastClickTime = Time.unscaledTime;
    if (doubleClick && edit(_session.Frames[hovered].Target))
      return true;
    IOverlayLayoutTarget target = _session.Frames[hovered].Target;
    // Pressing one of several selected elements moves them all.
    if (_selection.Multi && _selection.Contains(target))
    {
      _selection.Selected = hovered;
      _collapseTo = hovered;
      BeginGroup();
    }
    else
      _selection.SelectOnly(hovered);
    _dragging = hovered;
    _handle = handle;
    _start = target.Bounds;
    _pointerStart = CanvasSession.PointerFor(target, mouse);
    _pressPosition = mouse;
    _pressMoved = false;
    _scale = CanvasSession.ValidScale(target.EffectiveScale);
    return false;
  }

  public void Continue(Vector2 mouse)
  {
    CanvasFrame frame = _session.Frames[_dragging];
    if ((mouse - _pressPosition).sqrMagnitude > 9)
      _pressMoved = true;
    if (frame.Target.Available && Input.GetMouseButton(0) && _pressMoved)
    {
      if (GroupDragging)
        DragGroup(mouse);
      else
        DragBox(frame, mouse);
    }
    _cursors.Set(_handle);
    if (!Input.GetMouseButton(0))
      Release();
  }

  public void Release()
  {
    // A click on one of several selected elements, without moving, selects it alone.
    int collapse = _pressMoved ? -1 : _collapseTo;
    Finish();
    if (collapse >= 0)
      _selection.SelectOnly(collapse);
    _guides.HideLines();
  }

  /// <summary>Ends a drag: what moved is placed for good, as one undo step.</summary>
  public void Finish()
  {
    _collapseTo = -1;
    if (_dragging < 0 || _dragging >= _session.Frames.Count)
    {
      _groupTargets.Clear();
      return;
    }
    if (GroupDragging)
    {
      // A press that didn't move leaves every element where it was saved.
      if (_pressMoved)
        foreach (IOverlayLayoutTarget member in _groupTargets)
          member.CommitLayout(true);
    }
    else
      _session.Frames[_dragging].Target.CommitLayout(_handle == LayoutHandle.Move);
    _groupTargets.Clear();
    _groupStart.Clear();
    _dragging = -1;
    _session.Commit();
  }

  /// <summary>Drops the drag without placing anything: it began in another view.</summary>
  public void Cancel() => _dragging = -1;

  private void DragBox(CanvasFrame frame, Vector2 mouse)
  {
    Vector2 pointer = CanvasSession.PointerFor(frame.Target, mouse);
    float dx = pointer.x - _pointerStart.x,
      dy = pointer.y - _pointerStart.y;
    LayoutBounds resized = OverlayLayoutGeometry.Drag(
      CanvasSession.ToBounds(_start),
      _handle,
      dx,
      dy,
      Screen.width,
      Screen.height,
      Mathf.Min(1f, 0.2f / _scale),
      Mathf.Max(1f, CanvasSession.MaximumScale / _scale)
    );
    if (_handle == LayoutHandle.Move && _session.Preferences.LayoutSnapping && !EditorKeys.AltHeld)
    {
      _snapReferences.Clear();
      List<CanvasFrame> frames = _session.Frames;
      for (int i = 0; i < frames.Count; i++)
        if (i != _dragging && frames[i].Target.Available && frames[i].Target.Enabled)
          _snapReferences.Add(CanvasSession.ToBounds(frames[i].Target.Bounds));
      resized = _guides.Snap(resized, _snapReferences, 7 * _session.UiScale);
    }
    else
      _guides.HideLines();
    frame.Target.SetBounds(CanvasSession.ToRect(resized));
  }

  // ---- Moving several at once ----

  private void BeginGroup()
  {
    _groupTargets.Clear();
    _groupStart.Clear();
    foreach (IOverlayLayoutTarget target in _selection.Targets)
    {
      _groupTargets.Add(target);
      _groupStart.Add(target.Bounds);
    }
  }

  private bool GroupDragging => _groupTargets.Count > 1;

  private void DragGroup(Vector2 mouse)
  {
    IOverlayLayoutTarget primary = _session.Frames[_dragging].Target;
    Vector2 pointer = CanvasSession.PointerFor(primary, mouse);
    float dx = pointer.x - _pointerStart.x,
      dy = pointer.y - _pointerStart.y;
    ClampGroupMove(_groupStart, ref dx, ref dy);
    if (_session.Preferences.LayoutSnapping && !EditorKeys.AltHeld)
    {
      _groupBounds.Clear();
      foreach (Rect start in _groupStart)
        _groupBounds.Add(new LayoutBounds(start.x + dx, start.y + dy, start.width, start.height));
      LayoutBounds group = LayoutArrange.Union(_groupBounds);
      _snapReferences.Clear();
      List<CanvasFrame> frames = _session.Frames;
      for (int i = 0; i < frames.Count; i++)
        if (CanvasSelection.Selectable(frames[i].Target) && !_groupTargets.Contains(frames[i].Target))
          _snapReferences.Add(CanvasSession.ToBounds(frames[i].Target.Bounds));
      LayoutBounds snapped = _guides.Snap(group, _snapReferences, 7 * _session.UiScale);
      dx += snapped.X - group.X;
      dy += snapped.Y - group.Y;
      ClampGroupMove(_groupStart, ref dx, ref dy);
    }
    else
      _guides.HideLines();
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
        CanvasSession.ToBounds(start),
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

  /// <summary>Arrow keys move every selected element by the same step.</summary>
  public void Nudge(float dx, float dy)
  {
    List<IOverlayLayoutTarget> selection = _selection.Targets;
    _groupStart.Clear();
    foreach (IOverlayLayoutTarget target in selection)
      _groupStart.Add(target.Bounds);
    ClampGroupMove(_groupStart, ref dx, ref dy);
    for (int i = 0; i < selection.Count; i++)
    {
      Rect start = _groupStart[i];
      selection[i].SetBounds(new Rect(start.x + dx, start.y + dy, start.width, start.height));
      selection[i].CommitLayout(true);
    }
    _groupStart.Clear();
  }

  // Dragging an edge resizes one selected element, except those sized in
  // their editor. Several selected only move.
  public bool Resizable(IOverlayLayoutTarget target) => !_selection.Multi && !_sizedInEditor(target);

  /// <summary>The element under <paramref name="pointer"/> and the handle there; -1 for none.</summary>
  public int FindTarget(Vector2 pointer, out LayoutHandle bestHandle)
  {
    List<CanvasFrame> frames = _session.Frames;
    // The selected rectangle owns its editing area, including where another
    // element's compact content overlaps it. New copies must remain draggable.
    if (_selection.Multi)
    {
      // Several selected: each only moves, and the smallest under the pointer wins.
      int owner = -1;
      float smallest = float.MaxValue;
      foreach (IOverlayLayoutTarget member in _selection.Targets)
      {
        Rect bounds = member.Bounds;
        Vector2 local = CanvasSession.PointerFor(member, pointer);
        if (!bounds.Contains(local) || bounds.width * bounds.height >= smallest)
          continue;
        owner = _session.IndexOf(member);
        smallest = bounds.width * bounds.height;
      }
      if (owner >= 0)
      {
        bestHandle = LayoutHandle.Move;
        return owner;
      }
    }
    else if (_selection.HasSelection)
    {
      IOverlayLayoutTarget selected = frames[_selection.Selected].Target;
      Vector2 selectedPointer = CanvasSession.PointerFor(selected, pointer);
      bool sized = _sizedInEditor(selected);
      bestHandle = OverlayLayoutGeometry.HitTest(
        CanvasSession.ToBounds(selected.Bounds),
        selectedPointer.x,
        selectedPointer.y,
        sized ? 0 : 10 * _session.UiScale
      );
      // Elements sized in their editor only move.
      if (bestHandle != LayoutHandle.None && sized)
        bestHandle = LayoutHandle.Move;
      if (bestHandle != LayoutHandle.None)
        return _selection.Selected;
    }
    int best = -1;
    float bestArea = float.MaxValue;
    bestHandle = LayoutHandle.None;
    for (int index = 0; index < frames.Count; index++)
    {
      IOverlayLayoutTarget target = frames[index].Target;
      if (!target.Available || !target.Enabled)
        continue;
      Rect bounds = index == _selection.Selected ? target.Bounds : target.SelectionBounds;
      if (bounds.width <= 0 || bounds.height <= 0)
        continue;
      Vector2 localPointer = CanvasSession.PointerFor(target, pointer);
      bool resizable = index == _selection.Selected && Resizable(target);
      LayoutHandle handle = OverlayLayoutGeometry.HitTest(
        CanvasSession.ToBounds(bounds),
        localPointer.x,
        localPointer.y,
        resizable ? 10 * _session.UiScale : 0
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
}
