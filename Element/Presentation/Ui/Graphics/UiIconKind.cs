namespace Element.Presentation.Ui.Graphics;

/// <summary>Every icon <see cref="UiIcon"/> draws. Each one is its own glyph; none is drawn by another's code.</summary>
internal enum UiIconKind
{
  Undo,
  Redo,
  Reset,
  Snap,
  Plus,
  Minus,
  Grip,
  Delete,
  Close,
  Text,
  KeyViewer,
  Shape,
  Square,
  Triangle,
  Circle,
  Backspace,
  Back,
  Forward,
  GrowFrom,
  StrokeWidth,
  AlignLeft,
  AlignCenter,
  AlignRight,
  AlignTop,
  AlignMiddle,
  AlignBottom,
  DistributeHorizontal,
  DistributeVertical,
  Font,
  Animate,

  /// <summary>One way in that parts into two: an If.</summary>
  If,
  Extension,
  Check,
  Up,
  Down,

  /// <summary>The settings window's categories, drawn larger.</summary>
  Overlay,
  Tweaks,
  General,
  Effects,

  /// <summary>A chevron pointing right, on a link that opens a page.</summary>
  PageForward,

  /// <summary>The settings window's Back button.</summary>
  PageBack,

  /// <summary>The close button of a window or a picker.</summary>
  WindowClose,

  /// <summary>Two arrows passing each other: flips a gradient.</summary>
  Flip,

  /// <summary>Most of a circle with an arrow on its end: turns a gradient 90°.</summary>
  Rotate,

  /// <summary>A bin with its lid, beside a gradient's picked color.</summary>
  RemoveStop,

  /// <summary>A second sheet behind the first.</summary>
  Duplicate,

  /// <summary>A pencil.</summary>
  Edit,

  /// <summary>An arrow out of a tray, like the system's share icon.</summary>
  Export,

  /// <summary>A magnifying glass, leading a search field.</summary>
  Search,
}
