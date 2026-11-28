namespace Element.Domain.Overlay.Values;

// What a value is to arithmetic: a plain number, a time in seconds shown as
// m:ss, words that can only be shown as they are, or a color that can only
// color something.
internal enum OverlayValueKind
{
  Number,
  Time,
  Text,
  Color,

  /// <summary>True or false.</summary>
  Bool,

  /// <summary>Whatever is put in it: a condition, a group.</summary>
  Any,

  /// <summary>One of &lt; ≤ = ≠ ≥ &gt;, which a condition compares by.</summary>
  Compare,

  /// <summary>A point of an element's box, or Auto.</summary>
  Point,
}
