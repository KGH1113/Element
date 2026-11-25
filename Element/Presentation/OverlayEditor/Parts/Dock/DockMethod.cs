namespace Element.Presentation.OverlayEditor.Parts.Dock;

// The input method the dock shows: what the picked piece is edited with.
internal enum DockMethod
{
  /// <summary>Numbers: the keypad.</summary>
  Keypad,

  /// <summary>Words: typed on the keyboard.</summary>
  Words,

  /// <summary>True or false.</summary>
  Switch,

  /// <summary>A condition's operator: the six comparisons.</summary>
  Compare,

  /// <summary>A condition: its five slots, each opened in turn.</summary>
  Condition,

  /// <summary>A group: the variable inside it.</summary>
  Group,

  /// <summary>A color: the color picker above.</summary>
  Color,

  /// <summary>A value the player made: its name, and its variable to go into.</summary>
  Custom,
}
