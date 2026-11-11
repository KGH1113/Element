using System;

namespace Element.Application.Overlay;

/// <summary>
/// The level name ADOFAI shows, which the overlay shows in its own place instead.
/// While active, the game's own stays hidden and its text comes to the follower.
/// </summary>
internal interface ISongTitleMirror
{
  /// <summary><paramref name="changed"/> gets the title and the game's font size for it, each time either changes.</summary>
  void Follow(Action<string, int> changed);

  void SetActive(bool active);

  /// <summary>Gives the game its level name back for good.</summary>
  void Release();
}
