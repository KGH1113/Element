using System;
using System.Collections.Generic;

namespace Element.Configuration.Overlay;

// The gradient's three handles, as Figma keeps them: where it starts (the
// center for radial, angular and diamond), where it ends, and how wide it
// reaches across. They are fractions of the element's box, y down. A linear
// gradient doesn't use the width handle. AngleFormula, in degrees with 90
// running left to right, turns the gradient with a live value.
[Serializable]
[CopiedInPlace]
public sealed class OverlayGradient
{
  public const int MaximumStops = 8;

  public OverlayGradientKind Kind = OverlayGradientKind.Linear;
  public List<OverlayGradientStop> Stops = new();
  public float StartX;
  public float StartY = .5f;
  public float EndX = 1;
  public float EndY = .5f;
  public float WidthX;
  public float WidthY = 1;
  public string AngleFormula = string.Empty;

  public OverlayGradient Clone()
  {
    OverlayGradient copy = new()
    {
      Kind = Kind,
      StartX = StartX,
      StartY = StartY,
      EndX = EndX,
      EndY = EndY,
      WidthX = WidthX,
      WidthY = WidthY,
      AngleFormula = AngleFormula ?? string.Empty,
    };
    if (Stops != null)
      foreach (OverlayGradientStop stop in Stops)
        if (stop != null)
          copy.Stops.Add(stop.Clone());
    return copy;
  }

  public bool SameAs(OverlayGradient other, bool highlight)
  {
    if (
      other == null
      || Kind != other.Kind
      || StartX != other.StartX
      || StartY != other.StartY
      || EndX != other.EndX
      || EndY != other.EndY
      || WidthX != other.WidthX
      || WidthY != other.WidthY
      || !OverlayColorSettings.SameFormula(AngleFormula, other.AngleFormula)
    )
      return false;
    int count = Stops?.Count ?? 0;
    if (count != (other.Stops?.Count ?? 0))
      return false;
    for (int i = 0; i < count; i++)
      if (!(Stops[i]?.SameAs(other.Stops[i], highlight) ?? other.Stops[i] == null))
        return false;
    return true;
  }
}
