using System;
using Element.Configuration.Overlay;
using Element.Domain.Overlay.Values;

namespace Element.Domain.Overlay.ValueColors;

/// <summary>
/// How a color that starts following a value first looks, by what the value
/// is. X-Accuracy steps up like a rank: the text's own color from 95, a soft
/// green from 98, sky blue from 99.5 and the highlight color at 100. Progress
/// blends from the text's own color at 0 into the highlight color at 100.
/// Anything else blends the same two colors across a tidy range that has
/// today's value in its lower part, so the change shows as it climbs.
/// </summary>
internal static class OverlayValueColorDefaults
{
  public const string Green = "#8EE07A",
    Sky = "#5AC8FA";

  private static readonly double[] RoundSteps = { 1, 1.5, 2, 2.5, 3, 4, 5, 6, 8, 10 };

  /// <summary>
  /// A new color following <paramref name="value"/> (null while none is
  /// picked), whose number is <paramref name="live"/> now (NaN for none),
  /// starting from the text's <paramref name="color"/>.
  /// </summary>
  public static OverlayValueColor For(OverlayValueDefinition value, double live, OverlayColorSettings color)
  {
    OverlayValueColor made = new() { Value = value?.Key ?? string.Empty };
    float alpha = Clamp01(color?.Alpha ?? 1);
    switch (value?.Id)
    {
      case OverlayValueId.Accuracy:
        made.Mode = OverlayValueColorMode.Step;
        made.Stops.Add(Own(95, color));
        made.Stops.Add(OverlayValueColorStop.Custom(98, Green, alpha));
        made.Stops.Add(OverlayValueColorStop.Custom(99.5f, Sky, alpha));
        made.Stops.Add(OverlayValueColorStop.HighlightColor(100, alpha));
        return made;
      case OverlayValueId.ProgressCurrent:
      case OverlayValueId.ProgressStart:
        made.Stops.Add(Own(0, color));
        made.Stops.Add(OverlayValueColorStop.HighlightColor(100, alpha));
        return made;
    }
    Fit(live, out float low, out float high);
    made.Stops.Add(Own(low, color));
    made.Stops.Add(OverlayValueColorStop.HighlightColor(high, alpha));
    return made;
  }

  /// <summary>
  /// A tidy range for a value at <paramref name="live"/>: from zero to a round
  /// number half again as far, so the value sits in its lower part; 0–100
  /// without a number.
  /// </summary>
  public static void Fit(double live, out float low, out float high)
  {
    low = 0;
    high = 100;
    if (double.IsNaN(live) || double.IsInfinity(live))
      return;
    float reach = NiceCeiling((float)Math.Max(Math.Abs(live) * 1.5, 10));
    if (live < 0)
    {
      low = -reach;
      high = 0;
    }
    else
      high = reach;
  }

  /// <summary>The next round number up from <paramref name="value"/>: 1, 1.5, 2, 2.5, 3, 4, 5, 6 or 8 times a power of ten.</summary>
  public static float NiceCeiling(float value)
  {
    if (value <= 0 || float.IsNaN(value) || float.IsInfinity(value))
      return 1;
    double power = Math.Pow(10, Math.Floor(Math.Log10(value)));
    foreach (double step in RoundSteps)
      if (step * power >= value - power * 1e-6)
        return (float)(step * power);
    return (float)(10 * power);
  }

  // The text's own color at the low end; one following the highlight starts
  // from white, so the climb into the highlight shows.
  private static OverlayValueColorStop Own(float at, OverlayColorSettings color)
  {
    float alpha = Clamp01(color?.Alpha ?? 1);
    if (color == null || color.Highlight || string.IsNullOrEmpty(color.Hex))
      return OverlayValueColorStop.Custom(at, "#FFFFFF", alpha);
    return OverlayValueColorStop.Custom(at, color.Hex, alpha);
  }

  private static float Clamp01(float value) =>
    value < 0 ? 0
    : value > 1 ? 1
    : value;
}
