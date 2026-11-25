using System;

namespace Element.Configuration.Overlay;

// One comparison of an If: Value against Than, both formulas (usually one
// value's key and a plain number, such as "accuracy" and "100"). Comparing
// them comes out one of three ways, and the switches say which of them turn
// the condition on: Value smaller than Than, the same, or bigger. Two on read
// as "or" (Same and Bigger: 100 or more). It is off while either side has no
// data.
[Serializable]
[CopiedInPlace]
public sealed class OverlayCondition
{
  public string Value = string.Empty;
  public string Than = string.Empty;
  public bool Smaller;
  public bool Same;
  public bool Bigger;

  public OverlayCondition Clone() =>
    new()
    {
      Value = Value ?? string.Empty,
      Than = Than ?? string.Empty,
      Smaller = Smaller,
      Same = Same,
      Bigger = Bigger,
    };

  public bool SameAs(OverlayCondition other) =>
    other != null
    && Smaller == other.Smaller
    && Same == other.Same
    && Bigger == other.Bigger
    && OverlayColorSettings.SameFormula(Value, other.Value)
    && OverlayColorSettings.SameFormula(Than, other.Than);
}
