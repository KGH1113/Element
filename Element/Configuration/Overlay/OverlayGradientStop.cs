using System;

namespace Element.Configuration.Overlay;

// Position is 0–1 along the gradient; its formula gives percent. Alpha's
// formula gives percent too. Hex is unused while the color follows the
// highlight.
[Serializable]
[CopiedInPlace]
public sealed class OverlayGradientStop
{
  public float Position;
  public string PositionFormula = string.Empty;
  public string Hex = "#FFFFFF";
  public float Alpha = 1f;
  public string AlphaFormula = string.Empty;

  public OverlayGradientStop Clone() =>
    new()
    {
      Position = Position,
      PositionFormula = PositionFormula ?? string.Empty,
      Hex = Hex,
      Alpha = Alpha,
      AlphaFormula = AlphaFormula ?? string.Empty,
    };

  public bool SameAs(OverlayGradientStop other, bool highlight) =>
    other != null
    && Position == other.Position
    && OverlayColorSettings.SameFormula(PositionFormula, other.PositionFormula)
    && Alpha == other.Alpha
    && OverlayColorSettings.SameFormula(AlphaFormula, other.AlphaFormula)
    && (highlight || Hex == other.Hex);
}
