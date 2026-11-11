namespace Element.Application.Gameplay;

/// <summary>What the game is doing now, as far as showing the overlay depends on it.</summary>
internal interface IPlayState
{
  /// <summary>Two players share the keyboard; the overlay and key viewers stay hidden.</summary>
  bool IsCoop { get; }

  /// <summary>
  /// A world the player moves in is running and not paused: a level, but also level
  /// select and the custom level select. Menus and the paused game are not.
  /// </summary>
  bool IsInGameWorld { get; }
}
