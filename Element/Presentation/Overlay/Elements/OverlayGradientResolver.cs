using System.Collections.Generic;
using Element.Configuration;
using Element.Domain.Elements;
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

/// <summary>
/// One color's gradient formulas, parsed once when settings change: each
/// stop's position and opacity, and the angle. Shapes and texts share it.
/// </summary>
internal sealed class OverlayGradientFormulas
{
  private readonly List<OverlayFormula> _positions = new(OverlayGradient.MaximumStops);
  private readonly List<OverlayFormula> _alphas = new(OverlayGradient.MaximumStops);
  private OverlayFormula _angle;

  /// <summary>The values the gradient uses, as catalog bits.</summary>
  public ulong Mask { get; private set; }

  public void Compile(OverlayColorSettings color)
  {
    _positions.Clear();
    _alphas.Clear();
    _angle = null;
    Mask = 0;
    if (color == null || !color.IsGradient)
      return;
    foreach (OverlayGradientStop stop in color.Gradient.Stops)
    {
      OverlayFormula position = OverlayFormulaText.Compile(stop.PositionFormula);
      OverlayFormula alpha = OverlayFormulaText.Compile(stop.AlphaFormula);
      _positions.Add(position);
      _alphas.Add(alpha);
      Mask |= (position?.Mask ?? 0) | (alpha?.Mask ?? 0);
    }
    _angle = OverlayFormulaText.Compile(color.Gradient.AngleFormula);
    Mask |= _angle?.Mask ?? 0;
  }

  /// <summary>
  /// Works out <paramref name="color"/>'s gradient into <paramref name="output"/>
  /// for a box <paramref name="width"/> by <paramref name="height"/> units;
  /// false for a solid color. Compile must have seen the same color.
  /// </summary>
  public bool Resolve(
    OverlayColorSettings color,
    Color highlight,
    IOverlayNumberSource source,
    float width,
    float height,
    OverlayResolvedGradient output
  )
  {
    if (color == null || !color.IsGradient || _positions.Count != color.Gradient.Stops.Count)
      return false;
    OverlayGradient gradient = color.Gradient;
    OverlayResolvedGradient.CopyGeometry(gradient, output.Geometry);
    if (_angle != null)
    {
      double degrees = OverlayFormulaText.Resolve(_angle, source, double.NaN);
      if (!double.IsNaN(degrees) && !double.IsInfinity(degrees))
        OverlayGradientMath.SetAngle(output.Geometry, (float)(degrees % 360), width, height);
    }
    output.Stops.Clear();
    for (int i = 0; i < gradient.Stops.Count; i++)
    {
      OverlayGradientStop stop = gradient.Stops[i];
      float position = Percent(_positions[i], source, stop.Position);
      float alpha = Percent(_alphas[i], source, stop.Alpha);
      OverlayRgba rgba = color.Highlight
        ? new OverlayRgba(highlight.r, highlight.g, highlight.b, alpha)
        : OverlayRgba.Parse(stop.Hex, alpha);
      output.Stops.Add(new OverlayGradientSample(position, rgba));
    }
    OverlayGradientMath.Sort(output.Stops);
    return true;
  }

  // A percent formula as 0–1, or the plain value while it has no result.
  private static float Percent(OverlayFormula formula, IOverlayNumberSource source, float plain)
  {
    double percent = OverlayFormulaText.Resolve(formula, source, double.NaN);
    float value = double.IsNaN(percent) || double.IsInfinity(percent) ? plain : (float)(percent / 100);
    return Mathf.Clamp01(value);
  }
}
