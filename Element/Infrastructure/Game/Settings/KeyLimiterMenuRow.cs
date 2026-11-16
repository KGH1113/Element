namespace Element.Infrastructure.Game.Settings;

/// <summary>
/// ADOFAI's Key Limiter row in the pause settings while ElementNote's keys are in use:
/// it reads "ElementNote keys" instead of the player's saved keys, says where to change
/// them, and doesn't open the key editor, whose keys wouldn't apply. Main thread only.
/// The row is the game's own object, so it is kept across the mod being turned off and on.
/// </summary>
internal static class KeyLimiterMenuRow
{
  private const string RowName = "keyLimiter";

  // The game builds its rows once per scene and doesn't redraw them on open,
  // so the row is kept to redraw it when ElementNote's keys start or stop applying.
  // Unity reports both as null once their scene unloads.
  private static SettingsMenu _menu;
  private static PauseSettingButton _row;
  private static bool _focused;

  internal static bool IsRow(PauseSettingButton setting) => setting != null && setting.name == RowName;

  internal static void Track(SettingsMenu menu, PauseSettingButton row)
  {
    if (!ReferenceEquals(_row, row))
      _focused = false;
    _menu = menu;
    _row = row;
  }

  internal static void TrackFocus(PauseSettingButton setting, bool focused)
  {
    if (ReferenceEquals(setting, _row))
      _focused = focused;
  }

  /// <summary>Redraws the row, and its description when it's selected.</summary>
  /// <param name="elementNoteKeys">Whether ElementNote's keys apply now.</param>
  internal static void Refresh(bool elementNoteKeys)
  {
    if (_menu == null || _row == null)
      return;
    // ElementNote took over mid-edit: the keys being edited wouldn't apply.
    if (elementNoteKeys && ReferenceEquals(_menu.selectedKeysSetting, Persistence.keyLimiterKeys))
      _menu.selectedKeysSetting = null;
    _menu.UpdateSetting(_row, SettingsMenu.Interaction.Refresh);
    // A hidden menu stays quiet: setting a description moves the goat and can play its sound.
    if (_focused && _menu.isActiveAndEnabled && !_menu.editingKeys)
      _row.UpdateDescription();
  }
}
