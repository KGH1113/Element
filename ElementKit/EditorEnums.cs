namespace ElementKit;

/// <summary>One of nine points of a box, as Element's Grow from picker shows them.</summary>
public enum Anchor
{
  /// <summary>The top left corner.</summary>
  TopLeft,

  /// <summary>The middle of the top edge.</summary>
  Top,

  /// <summary>The top right corner.</summary>
  TopRight,

  /// <summary>The middle of the left edge.</summary>
  Left,

  /// <summary>The middle.</summary>
  Center,

  /// <summary>The middle of the right edge.</summary>
  Right,

  /// <summary>The bottom left corner.</summary>
  BottomLeft,

  /// <summary>The middle of the bottom edge.</summary>
  Bottom,

  /// <summary>The bottom right corner.</summary>
  BottomRight,
}

/// <summary>Icons drawn the way Element's toolbar draws its own.</summary>
public enum EditorIcon
{
  /// <summary>A plus.</summary>
  Add,

  /// <summary>A minus.</summary>
  Remove,

  /// <summary>A trash can.</summary>
  Delete,

  /// <summary>A circular arrow.</summary>
  Reset,

  /// <summary>An ×.</summary>
  Close,

  /// <summary>A chevron pointing left.</summary>
  Left,

  /// <summary>A chevron pointing right.</summary>
  Right,

  /// <summary>A chevron pointing up.</summary>
  Up,

  /// <summary>A chevron pointing down.</summary>
  Down,

  /// <summary>An arrow turning back.</summary>
  Undo,

  /// <summary>An arrow turning forward.</summary>
  Redo,

  /// <summary>A letter T.</summary>
  Text,

  /// <summary>"Aa".</summary>
  Font,
}
