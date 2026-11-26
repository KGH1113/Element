namespace Element.Presentation.OverlayEditor.Parts.Dock;

// The input method the dock shows: what the picked piece is edited with.
internal enum DockMethod
{
  /// <summary>Nothing is picked: the place says what shows there.</summary>
  Rest,

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

  /// <summary>A solid color: the color picker, standing in the keypad's place.</summary>
  Color,

  /// <summary>A gradient: its bigger tool opens above; here a note says so.</summary>
  Gradient,

  /// <summary>A font field: the font list, standing in the keypad's place.</summary>
  Fonts,

  /// <summary>A point of the element (where it grows from): the nine points and Auto.</summary>
  Points,
}
