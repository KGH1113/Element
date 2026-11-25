using Element.Application.Gameplay;

namespace Element.Infrastructure.Game.Play;

/// <summary>Where the player is in the game, read from ADOFAI.</summary>
internal sealed class GameplayState : IPlayState
{
  public bool IsCoop => scrController.coopMode;

  public bool IsInGameWorld =>
    ADOBase.controller != null
    && !ADOBase.controller.paused
    && ADOBase.conductor != null
    && ADOBase.conductor.isGameWorld;

  /// <summary>
  /// A level is running and not paused: playing, waiting for the first key, or
  /// on its result. Menus, level select, the custom level select, the pause menu
  /// and the editor while editing are not play.
  /// </summary>
  public bool IsPlaying => IsInGameWorld && !ADOBase.isLevelSelect && !ADOBase.isCLS;
}
