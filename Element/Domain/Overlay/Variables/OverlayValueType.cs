namespace Element.Domain.Overlay.Variables;

// What a variable holds, and what a value gives. Values of different types
// can't be calculated together; a variable only takes what its type allows.
internal enum OverlayValueType
{
  /// <summary>Anything, side by side as it shows: a text's content.</summary>
  Any,

  /// <summary>A number, or a time in seconds shown as m:ss.</summary>
  Number,

  /// <summary>Words.</summary>
  Text,

  Color,

  /// <summary>True or false.</summary>
  Bool,

  /// <summary>One of &lt; ≤ = ≠ ≥ &gt;: what a condition compares by.</summary>
  Compare,
}
