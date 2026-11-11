namespace Element.Infrastructure.Game.Judgement;

/// <summary>
/// The Hide Judgment Text row in the game's pause settings. The game draws its rows
/// once per scene, so the row is kept to redraw it after the mod changes the value.
/// Main thread only.
/// </summary>
internal static class HitTextHideMenuRow
{
  internal const string RowName = "hideHitMargin";
  internal const string CompetitiveModeRowName = "enableCompetitiveMode";

  // Unity reports both as null once their scene unloads.
  private static SettingsMenu _menu;
  private static PauseSettingButton _row;

  internal static void Track(SettingsMenu menu, PauseSettingButton row)
  {
    _menu = menu;
    _row = row;
  }

  internal static void Refresh()
  {
    if (_menu != null && _row != null)
      _menu.UpdateSetting(_row, SettingsMenu.Interaction.Refresh);
  }
}
