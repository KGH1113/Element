namespace Element.Domain.Overlay.Formula;

internal enum OverlayFormulaTokenKind
{
  Number,
  Value,

  /// <summary>+ − × ÷.</summary>
  Operator,

  /// <summary>&lt; ≤ = ≠ ≥ &gt;: the condition of a case.</summary>
  Compare,

  /// <summary>"Then": what follows is the case's result.</summary>
  Then,

  /// <summary>"Otherwise": another case follows.</summary>
  Else,

  /// <summary>A color picked by hand, as #RRGGBB.</summary>
  Color,
}
