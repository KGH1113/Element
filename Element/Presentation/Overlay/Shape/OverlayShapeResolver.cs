using System.Collections.Generic;
using Element.Configuration.Overlay;
using Element.Domain.Overlay.Shape;
using Element.Domain.Overlay.Values;
using Element.Domain.Overlay.Variables;
using Element.Presentation.Overlay.Elements;
using Element.Presentation.Overlay.Text;
using UnityEngine;

namespace Element.Presentation.Overlay.Shape;

/// <summary>
/// Every number of a shape that can follow a value, parsed once, and worked
/// out into a copy for drawing. Shared by the overlay and the shape editor.
/// </summary>
internal sealed class OverlayShapeResolver
{
  private OverlayVariable _width,
    _height,
    _stroke;
  private readonly List<OverlayVariable> _radius = new(4);
  private readonly OverlayGradientFormulas _fillGradient = new(),
    _strokeGradient = new();
  private readonly OverlayResolvedGradient _fillPaint = new(),
    _strokePaint = new();

  public OverlayValueMask Mask { get; private set; }
  public OverlayVariable FillAlpha { get; private set; }
  public OverlayVariable StrokeAlpha { get; private set; }

  /// <summary>Cases that give the fill another color while they hold; null for none.</summary>
  public OverlayVariable FillCases { get; private set; }

  /// <summary>Cases that give the border another color while they hold; null for none.</summary>
  public OverlayVariable StrokeCases { get; private set; }

  /// <summary>The fill's gradient as last resolved by <see cref="ResolvePaints"/>; null when solid.</summary>
  public OverlayResolvedGradient FillGradient { get; private set; }

  /// <summary>The border's gradient as last resolved; null when solid.</summary>
  public OverlayResolvedGradient StrokeGradient { get; private set; }

  public void Compile(OverlayShapeSettings shape)
  {
    _width = Parse(shape.WidthFormula);
    _height = Parse(shape.HeightFormula);
    _stroke = Parse(shape.StrokeWidthFormula);
    FillAlpha = Parse(shape.Fill?.AlphaFormula);
    StrokeAlpha = Parse(shape.Stroke?.AlphaFormula);
    FillCases = OverlayTextComposer.Cases(shape.Fill);
    StrokeCases = OverlayTextComposer.Cases(shape.Stroke);
    _fillGradient.Compile(shape.Fill);
    _strokeGradient.Compile(shape.Stroke);
    Mask =
      Bits(_width)
      | Bits(_height)
      | Bits(_stroke)
      | Bits(FillAlpha)
      | Bits(StrokeAlpha)
      | Bits(FillCases)
      | Bits(StrokeCases)
      | _fillGradient.Mask
      | _strokeGradient.Mask;
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

  /// <summary>
  /// Works out the fill's and the border's gradients for <paramref name="resolved"/>'s
  /// box into <see cref="FillGradient"/> and <see cref="StrokeGradient"/>. A
  /// case that holds gives a solid color instead.
  /// </summary>
  public void ResolvePaints(OverlayShapeSettings resolved, IOverlayNumberSource source, Color highlight)
  {
    FillGradient =
      !Holds(FillCases, source)
      && _fillGradient.Resolve(resolved.Fill, highlight, source, resolved.Width, resolved.Height, _fillPaint)
        ? _fillPaint
        : null;
    StrokeGradient =
      !Holds(StrokeCases, source)
      && _strokeGradient.Resolve(resolved.Stroke, highlight, source, resolved.Width, resolved.Height, _strokePaint)
        ? _strokePaint
        : null;
  }

  /// <summary>The fill as drawn now: its own color, or the one a case gives.</summary>
  public Color FillColor(OverlayShapeSettings resolved, IOverlayNumberSource source, Color highlight) =>
    OverlayTextComposer.Resolve(resolved.Fill, highlight, FillAlpha, source, FillCases, out _);

  /// <summary>The border as drawn now: its own color, or the one a case gives.</summary>
  public Color StrokeColor(OverlayShapeSettings resolved, IOverlayNumberSource source, Color highlight) =>
    OverlayTextComposer.Resolve(resolved.Stroke, highlight, StrokeAlpha, source, StrokeCases, out _);

  private static bool Holds(OverlayVariable cases, IOverlayNumberSource source) =>
    cases != null && cases.TryColor(source, out _);

  private static float Number(OverlayVariable formula, IOverlayNumberSource source, float fallback)
  {
    double value = OverlayFormulaText.Resolve(formula, source, fallback);
    return Mathf.Clamp((float)value, -100000, 100000);
  }

  private static OverlayVariable Parse(string text) => OverlayFormulaText.Compile(text);

  private static OverlayValueMask Bits(OverlayVariable formula) => OverlayTextComposer.Mask(formula);
}
