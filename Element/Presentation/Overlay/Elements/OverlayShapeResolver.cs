using System.Collections.Generic;
using Element.Configuration;
using Element.Domain.Elements;
using UnityEngine;

namespace Element.Presentation.Overlay.Elements;

/// <summary>
/// Every number of a shape that can follow a value, parsed once, and worked
/// out into a copy for drawing. Shared by the overlay and the shape editor.
/// </summary>
internal sealed class OverlayShapeResolver
{
  private OverlayFormula _width,
    _height,
    _stroke;
  private readonly List<OverlayFormula> _radius = new(4);

  public ulong Mask { get; private set; }
  public OverlayFormula FillAlpha { get; private set; }
  public OverlayFormula StrokeAlpha { get; private set; }

  public void Compile(OverlayShapeSettings shape)
  {
    _width = Parse(shape.WidthFormula);
    _height = Parse(shape.HeightFormula);
    _stroke = Parse(shape.StrokeWidthFormula);
    FillAlpha = Parse(shape.Fill?.AlphaFormula);
    StrokeAlpha = Parse(shape.Stroke?.AlphaFormula);
    Mask = Bits(_width) | Bits(_height) | Bits(_stroke) | Bits(FillAlpha) | Bits(StrokeAlpha);
    _radius.Clear();
    foreach (OverlayShapePoint point in shape.Points)
    {
      _radius.Add(Parse(point.RadiusFormula));
      Mask |= Bits(_radius[_radius.Count - 1]);
    }
  }

  /// <summary>
  /// Copies <paramref name="shape"/> into <paramref name="output"/> with every
  /// formula worked out. Formulas give pixels as drawn, so they are divided by
  /// the element's scale.
  /// </summary>
  public void Resolve(OverlayShapeSettings shape, IOverlayNumberSource source, float scale, OverlayShapeSettings output)
  {
    scale = Mathf.Max(.01f, scale);
    output.Kind = shape.Kind;
    output.Fill = shape.Fill;
    output.Stroke = shape.Stroke;
    output.Width = Mathf.Max(OverlayShapeGeometry.MinimumSize, Number(_width, source, shape.Width * scale) / scale);
    output.Height = Mathf.Max(OverlayShapeGeometry.MinimumSize, Number(_height, source, shape.Height * scale) / scale);
    output.StrokeWidth = Mathf.Max(0, Number(_stroke, source, shape.StrokeWidth * scale) / scale);
    while (output.Points.Count < shape.Points.Count)
      output.Points.Add(new OverlayShapePoint());
    if (output.Points.Count > shape.Points.Count)
      output.Points.RemoveRange(shape.Points.Count, output.Points.Count - shape.Points.Count);
    for (int i = 0; i < shape.Points.Count; i++)
    {
      OverlayShapePoint point = shape.Points[i];
      OverlayShapePoint resolved = output.Points[i];
      resolved.X = point.X;
      resolved.Y = point.Y;
      resolved.Radius = Mathf.Max(0, Number(_radius[i], source, point.Radius * scale) / scale);
    }
  }

  private static float Number(OverlayFormula formula, IOverlayNumberSource source, float fallback)
  {
    double value = OverlayFormulaText.Resolve(formula, source, fallback);
    return Mathf.Clamp((float)value, -100000, 100000);
  }

  private static OverlayFormula Parse(string text) => OverlayFormulaText.Compile(text);

  private static ulong Bits(OverlayFormula formula) => formula?.Mask ?? 0;
}
