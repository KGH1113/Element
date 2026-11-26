namespace Element.Presentation.OverlayEditor.Parts.Dock.Condition;

// How one of a condition's slots shows what it holds, in the condition's input method.
internal enum ConditionSlotLook : byte
{
  /// <summary>Nothing in it yet: its type's mark and its name.</summary>
  Empty,

  /// <summary>Its values as pills.</summary>
  Chips,

  /// <summary>One number or word of its own, written large.</summary>
  Big,

  /// <summary>One color: the color itself, large, over its name.</summary>
  Color,
}
