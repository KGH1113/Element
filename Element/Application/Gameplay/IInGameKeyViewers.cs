namespace Element.Application.Gameplay;

/// <summary>ElementNote's key viewers drawn inside the game window.</summary>
internal interface IInGameKeyViewers
{
  /// <summary>This system can draw them at all.</summary>
  bool Supported { get; }

  /// <summary>At least one viewer is on and ElementNote shows them in the game.</summary>
  bool HasEnabledViewers { get; }

  void SetPresence(KeyViewerPresence presence);
}
