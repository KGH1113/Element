using System;

namespace Element.Configuration.Overlay;

// X and Y are fractions of the shape's box; Radius is in reference units and
// its formula gives pixels as drawn. Corners sit where their kind puts them;
// shapes from 1.1.0 may keep corners that were dragged elsewhere.
[Serializable]
[CopiedInPlace]
public sealed class OverlayShapePoint
{
  public float X;
  public float Y;
  public float Radius;
  public string RadiusFormula = string.Empty;

  public OverlayShapePoint Clone() =>
    new()
    {
      X = X,
      Y = Y,
      Radius = Radius,
      RadiusFormula = RadiusFormula ?? string.Empty,
    };

  public bool SameAs(OverlayShapePoint other) =>
    other != null
    && X == other.X
    && Y == other.Y
    && Radius == other.Radius
    && OverlayColorSettings.SameFormula(RadiusFormula, other.RadiusFormula);
}
