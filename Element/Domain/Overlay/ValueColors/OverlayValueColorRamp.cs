using Element.Configuration.Overlay;
using Element.Domain.Overlay.Gradient;

namespace Element.Domain.Overlay.ValueColors;

/// <summary>
/// A color that follows a value, ready to work out: its stops sorted by value
/// with their colors read once, so finding the color for a number allocates
/// nothing and can run each time the value changes. Smoothly, the color
/// blends between the stops on either side of the number, as eyes see colors
/// (<see cref="OverlayOklab"/>); in steps, it is the color of the highest stop
/// the number has reached. Below the lowest stop and above the highest, their
/// colors hold.
/// </summary>
internal sealed class OverlayValueColorRamp
{
  private readonly float[] _at = new float[OverlayValueColor.MaximumStops];
  private readonly OverlayRgba[] _color = new OverlayRgba[OverlayValueColor.MaximumStops];
  private readonly bool[] _highlight = new bool[OverlayValueColor.MaximumStops];
  private readonly int[] _source = new int[OverlayValueColor.MaximumStops];

  public OverlayValueColorRamp() { }

  public OverlayValueColorRamp(OverlayValueColor color) => Load(color);

  /// <summary>How many stops it has, at most <see cref="OverlayValueColor.MaximumStops"/>.</summary>
  public int Count { get; private set; }

  public OverlayValueColorMode Mode { get; private set; }

  /// <summary>The lowest stop's value; zero without stops.</summary>
  public float Low => Count > 0 ? _at[0] : 0;

  /// <summary>The highest stop's value; zero without stops.</summary>
  public float High => Count > 0 ? _at[Count - 1] : 0;

  /// <summary>The value of the <paramref name="rank"/>th stop from the lowest.</summary>
  public float At(int rank) => _at[rank];

  /// <summary>Where the <paramref name="rank"/>th stop from the lowest is in the saved stops.</summary>
  public int Source(int rank) => _source[rank];

  /// <summary>
  /// Takes <paramref name="color"/>'s stops. Stops at the same value keep the
  /// order they were made in, so a hard edge between two never flips.
  /// </summary>
  public void Load(OverlayValueColor color)
  {
    Count = 0;
    Mode = color?.Mode ?? OverlayValueColorMode.Smooth;
    if (color?.Stops == null)
      return;
    for (int i = 0; i < color.Stops.Count && Count < _at.Length; i++)
    {
      OverlayValueColorStop stop = color.Stops[i];
      if (stop == null || float.IsNaN(stop.At) || float.IsInfinity(stop.At))
        continue;
      float alpha =
        stop.Alpha < 0 ? 0
        : stop.Alpha > 1 ? 1
        : stop.Alpha;
      int j = Count - 1;
      while (j >= 0 && _at[j] > stop.At)
      {
        _at[j + 1] = _at[j];
        _color[j + 1] = _color[j];
        _highlight[j + 1] = _highlight[j];
        _source[j + 1] = _source[j];
        j--;
      }
      _at[j + 1] = stop.At;
      _color[j + 1] = stop.Highlight ? new OverlayRgba(1, 1, 1, alpha) : OverlayRgba.Parse(stop.Hex, alpha);
      _highlight[j + 1] = stop.Highlight;
      _source[j + 1] = i;
      Count++;
    }
  }

  /// <summary>The <paramref name="rank"/>th stop's color from the lowest, with <paramref name="highlight"/> for one that follows it.</summary>
  public OverlayRgba Color(int rank, OverlayRgba highlight) =>
    _highlight[rank] ? highlight.WithAlpha(_color[rank].A) : _color[rank];

  /// <summary>Whether the <paramref name="rank"/>th stop from the lowest follows the highlight color.</summary>
  public bool FollowsHighlight(int rank) => _highlight[rank];

  /// <summary>
  /// The color at <paramref name="value"/>; <paramref name="highlight"/> is
  /// the highlight color for stops that follow it. White without stops.
  /// </summary>
  public OverlayRgba Evaluate(double value, OverlayRgba highlight)
  {
    if (Count == 0)
      return new OverlayRgba(1, 1, 1, 1);
    if (Mode == OverlayValueColorMode.Step || Count == 1)
      return Color(Reached(value), highlight);
    if (value <= _at[0])
      return Color(0, highlight);
    if (value >= _at[Count - 1])
      return Color(Count - 1, highlight);
    int below = Reached(value);
    float span = _at[below + 1] - _at[below];
    float t = span <= 1e-6f ? 1 : (float)((value - _at[below]) / span);
    return OverlayOklab.Mix(Color(below, highlight), Color(below + 1, highlight), t);
  }

  /// <summary>The highest stop <paramref name="value"/> has reached; the lowest one below them all.</summary>
  public int Reached(double value)
  {
    int reached = 0;
    for (int i = 1; i < Count; i++)
      if (_at[i] <= value)
        reached = i;
    return reached;
  }
}
