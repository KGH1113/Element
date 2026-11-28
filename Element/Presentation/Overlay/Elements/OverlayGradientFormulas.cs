using System.Collections.Generic;
using Element.Configuration.Overlay;
using Element.Domain.Overlay.Gradient;
using Element.Domain.Overlay.Values;
using Element.Domain.Overlay.Variables;
using Element.Presentation.Overlay.Text;
using UnityEngine;

namespace Element.Presentation.Overlay.Elements;

/// <summary>
/// One color's gradient formulas, parsed once when settings change: each
/// stop's position and opacity, and the angle. Shapes and texts share it.
/// </summary>
internal sealed class OverlayGradientFormulas
{
  private readonly List<OverlayVariable> _positions = new(OverlayGradient.MaximumStops);
  private readonly List<OverlayVariable> _alphas = new(OverlayGradient.MaximumStops);
  private OverlayVariable _angle;

  /// <summary>The values the gradient uses, by catalog id.</summary>
  public OverlayValueMask Mask { get; private set; }

  public void Compile(OverlayColorSettings color)
  {
    _positions.Clear();
    _alphas.Clear();
    _angle = null;
    Mask = OverlayValueMask.None;
    if (color == null || !color.IsGradient)
      return;
    foreach (OverlayGradientStop stop in color.Gradient.Stops)
    {
      OverlayVariable position = OverlayFormulaText.Compile(stop.PositionFormula);
      OverlayVariable alpha = OverlayFormulaText.Compile(stop.AlphaFormula);
      _positions.Add(position);
      _alphas.Add(alpha);
      Mask |= (position?.Mask ?? OverlayValueMask.None) | (alpha?.Mask ?? OverlayValueMask.None);
    }
    _angle = OverlayFormulaText.Compile(color.Gradient.AngleFormula);
    Mask |= _angle?.Mask ?? OverlayValueMask.None;
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
  private static float Percent(OverlayVariable formula, IOverlayNumberSource source, float plain)
  {
    double percent = OverlayFormulaText.Resolve(formula, source, double.NaN);
    float value = double.IsNaN(percent) || double.IsInfinity(percent) ? plain : (float)(percent / 100);
    return Mathf.Clamp01(value);
  }
}
