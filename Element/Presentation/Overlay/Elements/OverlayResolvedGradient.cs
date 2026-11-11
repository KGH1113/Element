using System.Collections.Generic;
using Element.Configuration.Overlay;
using Element.Domain.Overlay.Formula;
using Element.Domain.Overlay.Gradient;
using Element.Domain.Overlay.Values;
using UnityEngine;

namespace Element.Presentation.Overlay.Elements;

/// <summary>
/// A gradient as drawn right now: its handles with the angle's value worked
/// in, and its stops in order with their colors and opacities resolved.
/// </summary>
internal sealed class OverlayResolvedGradient
{
  public readonly OverlayGradient Geometry = new();
  public readonly List<OverlayGradientSample> Stops = new(OverlayGradient.MaximumStops);

  public OverlayGradientKind Kind => Geometry.Kind;

  public OverlayGradientFrame Frame(float width, float height) => OverlayGradientMath.Frame(Geometry, width, height);

  public OverlayRgba Sample(float t) => OverlayGradientMath.Sample(Stops, t);

  public Color32 Sample32(float t)
  {
    OverlayRgba color = Sample(t);
    return new Color32(Byte(color.R), Byte(color.G), Byte(color.B), Byte(color.A));
  }

  /// <summary>The first color, for small previews that show one color.</summary>
  public Color First
  {
    get
    {
      OverlayRgba color = Stops.Count > 0 ? Stops[0].Color : new OverlayRgba(1, 1, 1, 1);
      return new Color(color.R, color.G, color.B, color.A);
    }
  }

  public void CopyFrom(OverlayResolvedGradient other)
  {
    CopyGeometry(other.Geometry, Geometry);
    Stops.Clear();
    Stops.AddRange(other.Stops);
  }

  public bool SameAs(OverlayResolvedGradient other)
  {
    if (other == null || Stops.Count != other.Stops.Count || !SameGeometry(Geometry, other.Geometry))
      return false;
    for (int i = 0; i < Stops.Count; i++)
    {
      OverlayGradientSample a = Stops[i],
        b = other.Stops[i];
      if (
        a.Position != b.Position
        || a.Color.R != b.Color.R
        || a.Color.G != b.Color.G
        || a.Color.B != b.Color.B
        || a.Color.A != b.Color.A
      )
        return false;
    }
    return true;
  }

  internal static void CopyGeometry(OverlayGradient from, OverlayGradient to)
  {
    to.Kind = from.Kind;
    to.StartX = from.StartX;
    to.StartY = from.StartY;
    to.EndX = from.EndX;
    to.EndY = from.EndY;
    to.WidthX = from.WidthX;
    to.WidthY = from.WidthY;
  }

  private static bool SameGeometry(OverlayGradient a, OverlayGradient b) =>
    a.Kind == b.Kind
    && a.StartX == b.StartX
    && a.StartY == b.StartY
    && a.EndX == b.EndX
    && a.EndY == b.EndY
    && a.WidthX == b.WidthX
    && a.WidthY == b.WidthY;

  private static byte Byte(float channel) => (byte)Mathf.Clamp(Mathf.RoundToInt(channel * 255), 0, 255);
}
