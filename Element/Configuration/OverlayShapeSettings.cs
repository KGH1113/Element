using System;
using System.Collections.Generic;

namespace Element.Configuration;

// Width, Height and StrokeWidth are reference units (1920x1080) before the
// element's Scale, which grows the whole shape uniformly.
[Serializable]
[CopiedInPlace]
public sealed class OverlayShapeSettings
{
  public OverlayShapeKind Kind = OverlayShapeKind.Square;
  public float Width = 160f;
  public float Height = 160f;
  public List<OverlayShapePoint> Points = new();
  public OverlayColorSettings Fill = OverlayColorSettings.HighlightColor();
  public OverlayColorSettings Stroke = OverlayColorSettings.Custom("#FFFFFF");
  public float StrokeWidth;
  public string WidthFormula = string.Empty;
  public string HeightFormula = string.Empty;
  public string StrokeWidthFormula = string.Empty;

  public OverlayShapeSettings Clone()
  {
    OverlayShapeSettings copy = new()
    {
      Kind = Kind,
      Width = Width,
      Height = Height,
      Fill = (Fill ?? OverlayColorSettings.HighlightColor()).Clone(),
      Stroke = (Stroke ?? OverlayColorSettings.Custom("#FFFFFF")).Clone(),
      StrokeWidth = StrokeWidth,
      WidthFormula = WidthFormula ?? string.Empty,
      HeightFormula = HeightFormula ?? string.Empty,
      StrokeWidthFormula = StrokeWidthFormula ?? string.Empty,
    };
    if (Points != null)
      foreach (OverlayShapePoint point in Points)
        if (point != null)
          copy.Points.Add(point.Clone());
    return copy;
  }

  public bool SameAs(OverlayShapeSettings other)
  {
    if (
      other == null
      || Kind != other.Kind
      || Width != other.Width
      || Height != other.Height
      || StrokeWidth != other.StrokeWidth
      || !OverlayColorSettings.SameFormula(WidthFormula, other.WidthFormula)
      || !OverlayColorSettings.SameFormula(HeightFormula, other.HeightFormula)
      || !OverlayColorSettings.SameFormula(StrokeWidthFormula, other.StrokeWidthFormula)
      || !(Fill?.SameAs(other.Fill) ?? other.Fill == null)
      || !(Stroke?.SameAs(other.Stroke) ?? other.Stroke == null)
    )
      return false;
    int count = Points?.Count ?? 0;
    if (count != (other.Points?.Count ?? 0))
      return false;
    for (int i = 0; i < count; i++)
      if (!(Points[i]?.SameAs(other.Points[i]) ?? other.Points[i] == null))
        return false;
    return true;
  }
}
