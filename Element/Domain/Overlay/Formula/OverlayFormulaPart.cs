namespace Element.Domain.Overlay.Formula;

// Which part of a case the end of a formula is in. A formula with no
// condition is one Left part.
internal enum OverlayFormulaPart
{
  /// <summary>Before a comparison: the value compared, or the whole formula without a condition.</summary>
  Left,

  /// <summary>After a comparison: what it is compared with.</summary>
  Right,

  /// <summary>After "then": what the case gives.</summary>
  Result,
}
