namespace Element.Application.Gameplay;

/// <summary>
/// Where the in-game key viewers are: their pages loaded and hidden between plays,
/// so a play only has to show them, or on screen.
/// </summary>
internal enum KeyViewerPresence
{
  /// <summary>No pages: no viewer is on, or record mode keeps them out of the game.</summary>
  Off,

  /// <summary>Pages loaded and hidden, ready for the next play or the layout editor.</summary>
  Ready,

  /// <summary>On screen: during a play, or while the layout editor is open.</summary>
  Shown,
}
