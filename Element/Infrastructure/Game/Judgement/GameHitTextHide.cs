using System;
using Element.Application.Judgement;

namespace Element.Infrastructure.Game.Judgement;

/// <summary>ADOFAI's Hide Judgment Text, saved in the game's own settings file.</summary>
internal sealed class GameHitTextHide : IGameHitTextHide
{
  private readonly Action _changedInGame;
  private readonly Action _competitiveModeChanged;

  /// <param name="changedInGame">The player changed Hide Judgment Text in the game's pause settings.</param>
  /// <param name="competitiveModeChanged">The player switched Competitive Mode, which decides whether it applies.</param>
  public GameHitTextHide(Action changedInGame, Action competitiveModeChanged)
  {
    _changedInGame = changedInGame;
    _competitiveModeChanged = competitiveModeChanged;
  }

  public bool Applies => Persistence.enableCompetitiveMode;

  public HitMarginHidePreset Value
  {
    get => Persistence.hideHitMargin;
    set
    {
      Persistence.hideHitMargin = value;
      // The game batches this into one disk write shortly after the last change.
      Persistence.Save();
      HitTextHideMenuRow.Refresh();
    }
  }

  /// <summary>The game drew or changed a row of its pause settings.</summary>
  public void OnMenuRow(SettingsMenu menu, PauseSettingButton setting, SettingsMenu.Interaction action)
  {
    if (setting == null)
      return;
    string name = setting.name;
    if (name == HitTextHideMenuRow.RowName)
    {
      HitTextHideMenuRow.Track(menu, setting);
      if (action != SettingsMenu.Interaction.Refresh)
        _changedInGame?.Invoke();
    }
    else if (name == HitTextHideMenuRow.CompetitiveModeRowName && action != SettingsMenu.Interaction.Refresh)
      _competitiveModeChanged?.Invoke();
  }
}
