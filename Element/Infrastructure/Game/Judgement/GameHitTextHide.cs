using Element.Application.Judgement;
using Element.Bootstrap;
using HarmonyLib;

namespace Element.Infrastructure.Game.Judgement;

/// <summary>ADOFAI's Hide Judgment Text, saved in the game's own settings file.</summary>
internal sealed class GameHitTextHide : IGameHitTextHide
{
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
}

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

// Its own group: if a game update breaks this hook, judgment counting keeps working.
[HarmonyPatch(typeof(SettingsMenu), nameof(SettingsMenu.UpdateSetting))]
[PatchGroup("JudgmentTextSync")]
internal static class HitTextHideMenuPatch
{
  [HarmonyPostfix]
  private static void Postfix(SettingsMenu __instance, PauseSettingButton setting, SettingsMenu.Interaction action)
  {
    if (setting == null)
      return;
    string name = setting.name;
    if (name == HitTextHideMenuRow.RowName)
    {
      HitTextHideMenuRow.Track(__instance, setting);
      if (action != SettingsMenu.Interaction.Refresh)
        Main.Instance?.Runtime?.OnGameHitTextHideChanged();
    }
    else if (name == HitTextHideMenuRow.CompetitiveModeRowName && action != SettingsMenu.Interaction.Refresh)
      Main.Instance?.Runtime?.OnCompetitiveModeChanged();
  }
}
