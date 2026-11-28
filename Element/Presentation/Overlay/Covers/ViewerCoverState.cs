using System;
using System.Collections.Generic;
using Element.Application.Layout;
using Element.Domain.KeyViewer;
using Element.Domain.Layout;
using Element.Presentation.Overlay.Elements;

namespace Element.Presentation.Overlay.Covers;

/// <summary>
/// One key viewer as <see cref="KeyViewerCovers"/> last saw it: its frame on
/// screen, its place in the draw order, the pixels of each element in front of
/// it (kept until that element or the frame changes) and the rectangles it was
/// last given.
/// </summary>
internal sealed class ViewerCoverState
{
  private readonly Dictionary<IOverlayCover, ViewerCoverRuns> _runs = new();
  private readonly List<IOverlayCover> _dropped = new();

  public ViewerCoverState(IOverlayLayoutTarget target) => Target = target;

  public IOverlayLayoutTarget Target { get; }

  /// <summary>On, and its page or sample shows: only then do elements cover it.</summary>
  public bool Shown { get; private set; }

  /// <summary>Its frame in whole screen pixels, kept inside the screen.</summary>
  public int Left { get; private set; }

  public int Top { get; private set; }
  public int Right { get; private set; }
  public int Bottom { get; private set; }

  public LayoutBounds Frame => new(Left, Top, Right - Left, Bottom - Top);

  /// <summary>Back to front, from the draw order.</summary>
  public int Position { get; set; } = -1;

  /// <summary>Its covers must be worked out again.</summary>
  public bool Dirty { get; set; } = true;

  /// <summary>Still a key viewer; used while the list is matched again.</summary>
  public bool Alive { get; set; }

  /// <summary>The rectangles it was last given, in screen pixels.</summary>
  public List<LayoutBounds> Sent { get; } = new();

  /// <summary>Its covers must be sent even when they match <see cref="Sent"/>: the screen they were measured on changed.</summary>
  public bool Resend { get; set; }

  /// <summary>
  /// Reads whether it shows and where; true when either changed. A new frame
  /// forgets every element's pixels, which were clipped to the old one.
  /// </summary>
  public bool Refresh(bool shown, LayoutBounds bounds, int screenWidth, int screenHeight, Stack<ViewerCoverRuns> spare)
  {
    int left = 0,
      top = 0,
      right = 0,
      bottom = 0;
    if (shown)
    {
      left = Clamp(Floor(bounds.X), screenWidth);
      top = Clamp(Floor(bounds.Y), screenHeight);
      right = Clamp(Ceiling(bounds.Right), screenWidth);
      bottom = Clamp(Ceiling(bounds.Bottom), screenHeight);
      shown = right > left && bottom > top;
    }
    if (shown == Shown && left == Left && top == Top && right == Right && bottom == Bottom)
      return false;
    Shown = shown;
    Left = left;
    Top = top;
    Right = right;
    Bottom = bottom;
    Forget(spare);
    return true;
  }

  /// <summary>The pixels <paramref name="cover"/> drew over this viewer, from the spare ones when it has none yet.</summary>
  public ViewerCoverRuns RunsOf(IOverlayCover cover, Stack<ViewerCoverRuns> spare)
  {
    if (_runs.TryGetValue(cover, out ViewerCoverRuns runs))
      return runs;
    runs = spare.Count > 0 ? spare.Pop() : new ViewerCoverRuns();
    runs.Reset(Left, Top, Right, Bottom);
    _runs.Add(cover, runs);
    return runs;
  }

  /// <summary>Forgets the pixels of <paramref name="cover"/>, which no longer covers this viewer.</summary>
  public void Forget(IOverlayCover cover, Stack<ViewerCoverRuns> spare)
  {
    if (!_runs.TryGetValue(cover, out ViewerCoverRuns runs))
      return;
    _runs.Remove(cover);
    spare.Push(runs);
  }

  /// <summary>Forgets the pixels of every element whose state is not <see cref="ElementCoverState.Alive"/>.</summary>
  public void ForgetGone(Dictionary<IOverlayCover, ElementCoverState> elements, Stack<ViewerCoverRuns> spare)
  {
    _dropped.Clear();
    foreach (IOverlayCover cover in _runs.Keys)
      if (!elements.TryGetValue(cover, out ElementCoverState element) || !element.Alive)
        _dropped.Add(cover);
    foreach (IOverlayCover cover in _dropped)
      Forget(cover, spare);
  }

  /// <summary>Forgets every element's pixels.</summary>
  public void Forget(Stack<ViewerCoverRuns> spare)
  {
    foreach (ViewerCoverRuns runs in _runs.Values)
      spare.Push(runs);
    _runs.Clear();
  }

  private static int Floor(float value) => float.IsNaN(value) ? 0 : (int)Math.Max(int.MinValue / 2f, Math.Floor(value));

  private static int Ceiling(float value) =>
    float.IsNaN(value) ? 0 : (int)Math.Min(int.MaxValue / 2f, Math.Ceiling(value));

  private static int Clamp(int value, int size) => Math.Max(0, Math.Min(size, value));
}
