namespace Element.Infrastructure.Game.Lifecycle;

/// <summary>Whether the player is in the middle of a level.</summary>
internal static class GameplayState
{
  /// <summary>
  /// A level is running and not paused: playing, waiting for the first key, or
  /// on its result. Menus, level select, the pause menu and the editor while
  /// editing are not play.
  /// </summary>
  public static bool IsPlaying =>
    ADOBase.controller != null
    && !ADOBase.controller.paused
    && ADOBase.conductor != null
    && ADOBase.conductor.isGameWorld
    && !ADOBase.isLevelSelect
    && !ADOBase.isCLS;
}
