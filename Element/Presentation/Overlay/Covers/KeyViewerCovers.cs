using System;
using System.Collections.Generic;
using Element.Application.KeyViewer;
using Element.Application.Layout;
using Element.Domain.KeyViewer;
using Element.Domain.Layout;
using Element.Presentation.Overlay.Canvas;
using Element.Presentation.Overlay.Elements;
using UnityEngine;

namespace Element.Presentation.Overlay.Covers;

/// <summary>
/// Cuts the overlay elements drawn in front of a key viewer out of that viewer,
/// so the elements show where the viewer, drawn above the whole game, would
/// cover them. "In front" is a later place in the layout list
/// (<see cref="OverlayDrawOrder"/>, <see cref="ViewerCoverRule"/>); each
/// element marks its own pixels (<see cref="IOverlayCover"/>).
/// <para>
/// Runs once a frame after the canvases are laid out, so a text's new glyphs
/// are cut the frame they appear. It works only while the viewers show, and
/// then only on what changed: a viewer is worked out again when an element in
/// front of it that overlaps it changed (a combo text on a hit), when it moved,
/// or when the list, the screen or the canvas changed. Every element keeps
/// its pixels per viewer, so the others are not traced again. A frame where
/// nothing changed compares a few numbers per element and allocates nothing;
/// the viewers hear only of rectangles that differ from the last ones.
/// </para>
/// </summary>
internal sealed class KeyViewerCovers
{
  private readonly OverlayCanvasHost _canvas;
  private readonly IKeyViewerElements _viewers;
  private readonly INativeViewerLayer _layer;
  private readonly OverlayDrawOrder _order = new();
  private readonly List<ElementCoverState> _elements = new();
  private readonly Dictionary<IOverlayCover, ElementCoverState> _byCover = new();
  private readonly List<ElementCoverState> _goneElements = new();
  private readonly List<ViewerCoverState> _states = new();
  private readonly List<ViewerCoverState> _previousStates = new();
  private readonly Stack<ViewerCoverRuns> _spare = new();
  private readonly ViewerCoverMask _mask = new();
  private readonly List<ViewerCoverRuns> _covering = new();
  private readonly List<LayoutBounds> _spans = new();
  private int _screenWidth,
    _screenHeight;
  private bool _canvasShown;
  private bool _resting = true;

  public KeyViewerCovers(OverlayCanvasHost canvas, IKeyViewerElements viewers, INativeViewerLayer layer)
  {
    _canvas = canvas ?? throw new ArgumentNullException(nameof(canvas));
    _viewers = viewers ?? throw new ArgumentNullException(nameof(viewers));
    _layer = layer ?? throw new ArgumentNullException(nameof(layer));
  }

  /// <summary>Once a frame, after the canvases are laid out and before they draw.</summary>
  public void Tick()
  {
    if (!_layer.Showing)
    {
      // The viewers keep their covers while hidden; showing again checks everything.
      _resting = true;
      return;
    }
    bool all = _resting;
    _resting = false;
    if (Screen.width != _screenWidth || Screen.height != _screenHeight)
    {
      // The canvas scales with the screen: every element moved without saying so.
      // The viewers take covers relative to the screen, so all are sent again.
      _screenWidth = Screen.width;
      _screenHeight = Screen.height;
      foreach (ViewerCoverState state in _states)
      {
        state.Forget(_spare);
        state.Resend = true;
      }
      all = true;
    }
    bool canvasShown = _canvas.Root != null && _canvas.Root.activeInHierarchy;
    if (canvasShown != _canvasShown)
    {
      _canvasShown = canvasShown;
      all = true;
    }
    bool reordered = _order.Refresh(_canvas.LayoutSettings);
    reordered |= MatchElements();
    reordered |= MatchViewers();
    all |= reordered;
    ReadViewers(all);
    ReadElements(all);
    for (int i = 0; i < _states.Count; i++)
      if (_states[i].Dirty)
        Rebuild(_states[i]);
  }

  // The canvas's elements, in its order; true when they are not the ones of the last frame.
  private bool MatchElements()
  {
    IReadOnlyList<IOverlayLayoutTarget> targets = _canvas.LayoutTargets;
    bool same = targets.Count == _elements.Count;
    for (int i = 0; same && i < targets.Count; i++)
      same = ReferenceEquals(targets[i], _elements[i].Target);
    if (same)
      return false;
    foreach (ElementCoverState state in _elements)
      state.Alive = false;
    _elements.Clear();
    for (int i = 0; i < targets.Count; i++)
    {
      IOverlayLayoutTarget target = targets[i];
      ElementCoverState state =
        target is IOverlayCover cover && _byCover.TryGetValue(cover, out ElementCoverState known)
          ? known
          : new ElementCoverState(target);
      if (state.Cover != null)
        _byCover[state.Cover] = state;
      state.Alive = true;
      _elements.Add(state);
    }
    _goneElements.Clear();
    foreach (ElementCoverState state in _byCover.Values)
      if (!state.Alive)
        _goneElements.Add(state);
    foreach (ElementCoverState state in _goneElements)
      _byCover.Remove(state.Cover);
    foreach (ViewerCoverState viewer in _states)
      viewer.ForgetGone(_byCover, _spare);
    return true;
  }

  // The key viewers, in their order; true when they are not the ones of the last frame.
  private bool MatchViewers()
  {
    IReadOnlyList<IOverlayLayoutTarget> targets = _viewers.Targets;
    bool same = targets.Count == _states.Count;
    for (int i = 0; same && i < targets.Count; i++)
      same = ReferenceEquals(targets[i], _states[i].Target);
    if (same)
      return false;
    _previousStates.Clear();
    _previousStates.AddRange(_states);
    foreach (ViewerCoverState state in _previousStates)
      state.Alive = false;
    _states.Clear();
    for (int i = 0; i < targets.Count; i++)
    {
      ViewerCoverState state = Find(_previousStates, targets[i]) ?? new ViewerCoverState(targets[i]);
      state.Alive = true;
      _states.Add(state);
    }
    // A viewer that went takes its page with it; its pixels go back to the spares.
    foreach (ViewerCoverState state in _previousStates)
      if (!state.Alive)
        state.Forget(_spare);
    _previousStates.Clear();
    return true;
  }

  private static ViewerCoverState Find(List<ViewerCoverState> states, IOverlayLayoutTarget target)
  {
    foreach (ViewerCoverState state in states)
      if (ReferenceEquals(state.Target, target))
        return state;
    return null;
  }

  private void ReadViewers(bool all)
  {
    foreach (ViewerCoverState viewer in _states)
    {
      IOverlayLayoutTarget target = viewer.Target;
      bool shown = target.Enabled && target.Available;
      Rect bounds = shown ? target.Bounds : default;
      bool moved = viewer.Refresh(
        shown,
        new LayoutBounds(bounds.x, bounds.y, bounds.width, bounds.height),
        _screenWidth,
        _screenHeight,
        _spare
      );
      if (all)
        viewer.Position = _order.Position(target.Id);
      if (moved || all)
        viewer.Dirty = true;
    }
  }

  // Elements that changed make the viewers they covered or cover now dirty.
  private void ReadElements(bool all)
  {
    foreach (ElementCoverState element in _elements)
    {
      IOverlayCover cover = element.Cover;
      if (cover == null)
        continue;
      if (all)
        element.Position = _order.Position(cover.Id);
      int version = cover.CoverVersion;
      if (version == element.Version && !all)
        continue;
      element.Version = version;
      bool drewBefore = element.Drawn;
      LayoutBounds before = element.Bounds;
      element.Drawn = cover.TryGetCoverBounds(out LayoutBounds bounds);
      element.Bounds = bounds;
      if (all)
        continue;
      foreach (ViewerCoverState viewer in _states)
      {
        if (viewer.Dirty || !viewer.Shown || !ViewerCoverRule.InFront(element.Position, viewer.Position))
          continue;
        LayoutBounds frame = viewer.Frame;
        if (
          (drewBefore && ViewerCoverRule.Overlap(before, frame))
          || (element.Drawn && ViewerCoverRule.Overlap(bounds, frame))
        )
          viewer.Dirty = true;
      }
    }
  }

  // Traces what is new in front of the viewer, joins it with what is kept and
  // sends the rectangles when they differ from the last ones.
  private void Rebuild(ViewerCoverState viewer)
  {
    viewer.Dirty = false;
    _spans.Clear();
    _covering.Clear();
    int left = int.MaxValue,
      top = int.MaxValue,
      right = int.MinValue,
      bottom = int.MinValue;
    if (viewer.Shown && viewer.Position >= 0)
    {
      LayoutBounds frame = viewer.Frame;
      foreach (ElementCoverState element in _elements)
      {
        IOverlayCover cover = element.Cover;
        if (cover == null)
          continue;
        if (!element.Drawn || !ViewerCoverRule.Covers(element.Position, element.Bounds, viewer.Position, frame))
        {
          viewer.Forget(cover, _spare);
          continue;
        }
        ViewerCoverRuns runs = viewer.RunsOf(cover, _spare);
        if (runs.Stamp != element.Version)
        {
          runs.Reset(viewer.Left, viewer.Top, viewer.Right, viewer.Bottom);
          cover.Rasterize(runs);
          runs.Stamp = element.Version;
        }
        if (!runs.TryGetArea(out int runsLeft, out int runsTop, out int runsRight, out int runsBottom))
          continue;
        _covering.Add(runs);
        left = Math.Min(left, runsLeft);
        top = Math.Min(top, runsTop);
        right = Math.Max(right, runsRight);
        bottom = Math.Max(bottom, runsBottom);
      }
    }
    if (_covering.Count > 0)
    {
      // Joined only over the box the covers fill: a combo over a corner of the viewer costs that corner.
      _mask.Reset(left, top, right - left, bottom - top);
      foreach (ViewerCoverRuns runs in _covering)
        _mask.Add(runs);
      _mask.Spans(_spans);
      _covering.Clear();
    }
    if (!viewer.Resend && Same(_spans, viewer.Sent))
      return;
    viewer.Resend = false;
    viewer.Sent.Clear();
    for (int i = 0; i < _spans.Count; i++)
      viewer.Sent.Add(_spans[i]);
    _layer.SetCovers(viewer.Target.Id, viewer.Sent);
  }

  private static bool Same(List<LayoutBounds> a, List<LayoutBounds> b)
  {
    if (a.Count != b.Count)
      return false;
    for (int i = 0; i < a.Count; i++)
      if (a[i].X != b[i].X || a[i].Y != b[i].Y || a[i].Width != b[i].Width || a[i].Height != b[i].Height)
        return false;
    return true;
  }
}
