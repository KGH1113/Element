using System;
using System.Collections.Generic;
using Element.Configuration.Overlay;
using Element.Domain.Overlay.Gradient;
using Element.Domain.Overlay.Values;

namespace Element.Domain.Overlay.ValueColors;

/// <summary>
/// Changes to a color that follows a value, as the editors make them: adding
/// a stop where the color already is, removing one while two stay, moving
/// one to a tidy number, and following another value. Edits change the color
/// they are given; callers pass a copy.
/// </summary>
internal static class OverlayValueColorEdits
{
  private static readonly double[] StepFactors = { 1, 2, 5, 10 };

  /// <summary>
  /// The values its band spans: from the lowest stop to the highest, widened
  /// around a single value so the band never collapses.
  /// </summary>
  public static void Range(OverlayValueColor color, out float low, out float high)
  {
    low = float.MaxValue;
    high = float.MinValue;
    if (color?.Stops != null)
      foreach (OverlayValueColorStop stop in color.Stops)
      {
        if (stop == null || float.IsNaN(stop.At) || float.IsInfinity(stop.At))
          continue;
        low = Math.Min(low, stop.At);
        high = Math.Max(high, stop.At);
      }
    if (low > high)
    {
      low = 0;
      high = 100;
      return;
    }
    if (high - low < 1e-4f)
    {
      float pad = Math.Max(1, Math.Abs(high) * .1f);
      low -= pad;
      high += pad;
    }
  }

  /// <summary>
  /// The step stops snap to on a band from <paramref name="low"/> to
  /// <paramref name="high"/>: a round number (1, 2 or 5 times a power of ten)
  /// that splits it into about fifty.
  /// </summary>
  public static float Step(float low, float high)
  {
    float span = Math.Abs(high - low);
    if (span <= 0 || float.IsNaN(span) || float.IsInfinity(span))
      return 1;
    double rough = span / 50.0;
    double power = Math.Pow(10, Math.Floor(Math.Log10(rough)));
    foreach (double factor in StepFactors)
      if (factor * power >= rough - power * 1e-6)
        return (float)(factor * power);
    return (float)(10 * power);
  }

  /// <summary><paramref name="value"/> on the nearest step of the band, without float dust such as 98.30000001.</summary>
  public static float Snap(float value, float low, float high)
  {
    float step = Step(low, high);
    double snapped = Math.Round(value / (double)step) * step;
    int decimals = Math.Max(0, Math.Min(6, (int)Math.Ceiling(-Math.Log10(step) - 1e-6)));
    return (float)Math.Round(snapped, decimals);
  }

  /// <summary>The stops' places in <paramref name="color"/> from the lowest value up; stops at one value keep their order.</summary>
  public static List<int> Order(OverlayValueColor color)
  {
    List<int> order = new();
    if (color?.Stops == null)
      return order;
    for (int i = 0; i < color.Stops.Count; i++)
      order.Add(i);
    for (int i = 1; i < order.Count; i++)
    {
      int index = order[i];
      int j = i - 1;
      while (j >= 0 && color.Stops[order[j]].At > color.Stops[index].At)
      {
        order[j + 1] = order[j];
        j--;
      }
      order[j + 1] = index;
    }
    return order;
  }

  /// <summary>
  /// Adds a stop at <paramref name="at"/> with the color the band already
  /// has there, so nothing changes until it is moved or recolored. A stop
  /// that only takes over a neighbor's color takes it whole, following the
  /// highlight if that one does. Returns its place, or -1 when the color
  /// has as many stops as it can hold.
  /// </summary>
  public static int AddStop(OverlayValueColor color, float at, OverlayRgba highlight)
  {
    if (color == null || color.Stops.Count >= OverlayValueColor.MaximumStops)
      return -1;
    OverlayValueColorRamp ramp = new(color);
    OverlayValueColorStop stop;
    if (ramp.Count == 0)
      stop = OverlayValueColorStop.HighlightColor(at);
    else if (ramp.Mode == OverlayValueColorMode.Step || at <= ramp.Low || at >= ramp.High || ramp.Count == 1)
    {
      int rank =
        ramp.Mode == OverlayValueColorMode.Step ? ramp.Reached(at)
        : at <= ramp.Low ? 0
        : ramp.Count - 1;
      stop = color.Stops[ramp.Source(rank)].Clone();
      stop.At = at;
    }
    else
    {
      int below = ramp.Reached(at);
      OverlayRgba mixed = ramp.Evaluate(at, highlight);
      stop =
        ramp.FollowsHighlight(below) && ramp.FollowsHighlight(below + 1)
          ? OverlayValueColorStop.HighlightColor(at, mixed.A)
          : OverlayValueColorStop.Custom(at, mixed.ToHex(), mixed.A);
    }
    color.Stops.Add(stop);
    return color.Stops.Count - 1;
  }

  /// <summary>Removes the stop at <paramref name="index"/>; false while only two are left.</summary>
  public static bool RemoveStop(OverlayValueColor color, int index)
  {
    if (color == null || index < 0 || index >= color.Stops.Count || color.Stops.Count <= OverlayValueColor.MinimumStops)
      return false;
    color.Stops.RemoveAt(index);
    return true;
  }

  /// <summary>
  /// Spreads the stops over <paramref name="low"/>–<paramref name="high"/>
  /// keeping where each sits between the ends and its color, each on a tidy
  /// number.
  /// </summary>
  public static void Refit(OverlayValueColor color, float low, float high)
  {
    Range(color, out float oldLow, out float oldHigh);
    float span = oldHigh - oldLow;
    foreach (OverlayValueColorStop stop in color.Stops)
    {
      float t = span <= 1e-6f ? 0 : (stop.At - oldLow) / span;
      stop.At = Snap(low + (high - low) * t, low, high);
    }
  }

  /// <summary>The value <paramref name="color"/> follows; null while it follows none.</summary>
  public static OverlayValueDefinition Followed(OverlayValueColor color) =>
    color != null && OverlayValueCatalog.TryResolve(color.Value, out OverlayValueDefinition value) ? value : null;

  /// <summary>
  /// <paramref name="color"/> following <paramref name="value"/> instead.
  /// A color that followed nothing yet starts as that value's defaults; one
  /// that did keeps its colors, their order and how they are spread,
  /// across the new value's range.
  /// </summary>
  public static OverlayValueColor Follow(
    OverlayValueColor color,
    OverlayValueDefinition value,
    double live,
    OverlayColorSettings own
  )
  {
    OverlayValueColor made = OverlayValueColorDefaults.For(value, live, own);
    if (color == null || string.IsNullOrEmpty(color.Value) || color.Stops.Count < OverlayValueColor.MinimumStops)
      return made;
    OverlayValueColor moved = color.Clone();
    moved.Value = made.Value;
    Range(made, out float low, out float high);
    Refit(moved, low, high);
    return moved;
  }
}
