using Element.Infrastructure.Game.Input;
using HarmonyLib;

namespace Element.Infrastructure.Game.Patches;

/// <summary>
/// SettingsMenu: the game's pause settings rows. UpdateSetting has the key limiter's
/// prefix, then two postfixes at normal priority: the judgment text sync's group is
/// applied before the key limiter's, so it runs first. Postfixes run even when the
/// key limiter's prefix skipped the game's update.
/// </summary>
internal static class SettingsMenuPatches
{
  [HarmonyPatch(typeof(SettingsMenu), nameof(SettingsMenu.UpdateSetting))]
  [PatchGroup(PatchGroupNames.KeyLimiter)]
  private static class UpdateSettingForKeyLimiter
  {
    // Enter would open the editor for keys that don't apply; show why instead.
    [HarmonyPrefix]
    private static bool Prefix(PauseSettingButton setting, SettingsMenu.Interaction action)
    {
      if (action != SettingsMenu.Interaction.Activate || GameHooks.KeyLimiter?.OwnsMenuRow(setting) != true)
        return true;
      setting.UpdateDescription();
      return false;
    }

    [HarmonyPostfix]
    private static void Postfix(SettingsMenu __instance, PauseSettingButton setting, SettingsMenu.Interaction action)
    {
      if (!KeyLimiterMenuRow.IsRow(setting))
        return;
      KeyLimiterMenuRow.Track(__instance, setting);
      GameKeyLimiter limiter = GameHooks.KeyLimiter;
      if (action == SettingsMenu.Interaction.Refresh && limiter != null && limiter.Active)
        setting.valueLabel.text = limiter.MenuLabel;
    }
  }

  [HarmonyPatch(typeof(SettingsMenu), nameof(SettingsMenu.UpdateSetting))]
  [PatchGroup(PatchGroupNames.JudgmentTextSync)]
  private static class UpdateSettingForJudgmentText
  {
    [HarmonyPostfix]
    private static void Postfix(SettingsMenu __instance, PauseSettingButton setting, SettingsMenu.Interaction action) =>
      GameHooks.HitTextHide?.OnMenuRow(__instance, setting, action);
  }
}
