namespace Element.Domain.Overlay.Variables;

// What one piece of a variable is.
internal enum OverlayPieceKind
{
  /// <summary>A number as typed on the keypad: "12", "1.", "-3".</summary>
  Number,

  /// <summary>Words typed on the keyboard.</summary>
  Text,

  /// <summary>A color picked in the color picker, as #RRGGBB.</summary>
  Color,

  /// <summary>True or false.</summary>
  Bool,

  /// <summary>One of &lt; ≤ = ≠ ≥ &gt;.</summary>
  Compare,

  /// <summary>+ − × ÷ between two numbers.</summary>
  Operator,

  /// <summary>A live value: the game's, another mod's, or the highlight color.</summary>
  Value,

  /// <summary>A value the player made (<see cref="OverlayCustomValues"/>).</summary>
  Custom,

  /// <summary>Compares two numbers and gives its result or its default (five slots).</summary>
  Condition,

  /// <summary>A variable inside a variable: a calculation in a text, for one.</summary>
  Group,
}
