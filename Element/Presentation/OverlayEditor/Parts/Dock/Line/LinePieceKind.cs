namespace Element.Presentation.OverlayEditor.Parts.Dock.Line;

/// <summary>How a piece is laid out on the dock's line.</summary>
internal enum LinePieceKind
{
  /// <summary>A value as a chip: its mark (or swatch) and what it says.</summary>
  Chip,

  /// <summary>+ − × ÷ between numbers.</summary>
  Operator,

  /// <summary>A condition with its slots laid out in a row: if … ≥ … then … otherwise ….</summary>
  Condition,

  /// <summary>A calculation inside a text, boxed.</summary>
  Group,
}
