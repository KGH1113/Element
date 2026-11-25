namespace Element.Domain.Overlay.Variables;

// Why a variable can't be what its field needs.
internal enum OverlayTypeProblemKind
{
  /// <summary>Nothing in it, where something must be.</summary>
  Empty,

  /// <summary>A value of another type than the field takes.</summary>
  WrongType,

  /// <summary>Two values side by side where one value must be: join numbers with + − × ÷.</summary>
  SideBySide,

  /// <summary>Something that isn't a number in a calculation.</summary>
  NotNumber,

  /// <summary>An operator with no number after it.</summary>
  Dangling,

  /// <summary>A made value that uses itself.</summary>
  Loop,
}
