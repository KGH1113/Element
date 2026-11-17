using Element.Configuration.Overlay;
using Element.Domain.Overlay.Gradient;
using Element.Domain.Overlay.ValueColors;
using Element.Presentation.Overlay.Elements;
using UnityEngine;

namespace Element.Presentation.OverlayEditor.Parts.ValueColor;

/// <summary>
/// A color that follows a value in a swatch: its band, left to right, as a
/// gradient a swatch can show. Steps keep their sharp edges.
/// </summary>
internal static class ValueColorSwatch
{
  private const int Samples = 9;

  /// <summary>Fills <paramref name="target"/> with <paramref name="ramp"/>'s band; false without stops.</summary>
  public static bool Fill(OverlayResolvedGradient target, OverlayValueColorRamp ramp, Color highlight)
  {
    target.Stops.Clear();
    OverlayGradientMath.ResetHandles(target.Geometry, OverlayGradientKind.Linear);
    if (ramp == null || ramp.Count == 0)
      return false;
    OverlayRgba accent = new(highlight.r, highlight.g, highlight.b, 1);
    float low = ramp.Low,
      span = Mathf.Max(1e-6f, ramp.High - ramp.Low);
    if (ramp.Mode == OverlayValueColorMode.Step)
    {
      for (int rank = 0; rank < ramp.Count; rank++)
      {
        float from = rank == 0 ? 0 : (ramp.At(rank) - low) / span;
        float to = rank == ramp.Count - 1 ? 1 : (ramp.At(rank + 1) - low) / span;
        OverlayRgba color = ramp.Color(rank, accent);
        target.Stops.Add(new OverlayGradientSample(from, color));
        target.Stops.Add(new OverlayGradientSample(to, color));
      }
      return true;
    }
    for (int i = 0; i < Samples; i++)
    {
      float t = i / (Samples - 1f);
      target.Stops.Add(new OverlayGradientSample(t, ramp.Evaluate(low + span * t, accent)));
    }
    return true;
  }
}
