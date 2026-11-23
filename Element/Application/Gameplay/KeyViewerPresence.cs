namespace Element.Application.Gameplay;

/// <summary>
/// Where the in-game key viewers are: their pages loaded and hidden between plays,
/// so a play only has to show them, or on screen.
/// </summary>
internal enum KeyViewerPresence
{
  /// <summary>No pages: no viewer is on, ElementNote keeps them out of the game, or this system can't show them.</summary>
  Off,

  /// <summary>Pages loaded and hidden: between plays, in co-op and in record mode, ready to show at once.</summary>
  Ready,

  /// <summary>On screen: during a play, or while the layout editor is open.</summary>
  Shown,
}
