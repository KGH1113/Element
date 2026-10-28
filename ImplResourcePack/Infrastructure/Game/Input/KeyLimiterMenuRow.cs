using HarmonyLib;
using ImplResourcePack.Bootstrap;
using ImplResourcePack.Presentation.Settings;

namespace ImplResourcePack.Infrastructure.Game.Input;

/// <summary>
/// ADOFAI's Key Limiter row in the pause settings while ImplDmNote's keys are in use:
/// it reads "ImplDmNote keys" instead of the player's saved keys, says where to change
/// them, and doesn't open the key editor, whose keys wouldn't apply. Main thread only.
/// </summary>
internal static class KeyLimiterMenuRow
{
  private const string RowName = "keyLimiter";

  // The game builds its rows once per scene and doesn't redraw them on open,
  // so the row is kept to redraw it when ImplDmNote's keys start or stop applying.
  // Unity reports both as null once their scene unloads.
  private static SettingsMenu _menu;
  private static PauseSettingButton _row;
  private static bool _focused;

  internal static string Label => SettingsLocale.L("ImplDmNote 키", "ImplDmNote keys");

  internal static string Description =>
    SettingsLocale.L(
      "ImplDmNote가 연결되어 있는 동안에는 ImplDmNote의 키만 입력돼요. 직접 고른 키를 쓰려면 ImplDmNote에서 키 리미터를 꺼 주세요.",
      "While ImplDmNote is connected, only its keys are accepted. Turn off Enable Key Limiter in ImplDmNote to use your own keys."
    );

  internal static bool IsRow(PauseSettingButton setting) => setting != null && setting.name == RowName;

  internal static bool Owns(PauseSettingButton setting) => GameKeyLimiter.Active && IsRow(setting);

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
  internal static void Refresh()
  {
    if (_menu == null || _row == null)
      return;
    // ImplDmNote took over mid-edit: the keys being edited wouldn't apply.
    if (GameKeyLimiter.Active && ReferenceEquals(_menu.selectedKeysSetting, Persistence.keyLimiterKeys))
      _menu.selectedKeysSetting = null;
    _menu.UpdateSetting(_row, SettingsMenu.Interaction.Refresh);
    // A hidden menu stays quiet: setting a description moves the goat and can play its sound.
    if (_focused && _menu.isActiveAndEnabled && !_menu.editingKeys)
      _row.UpdateDescription();
  }
}

[HarmonyPatch(typeof(SettingsMenu), nameof(SettingsMenu.UpdateSetting))]
[PatchGroup("KeyLimiter")]
internal static class KeyLimiterMenuRowPatch
{
  // Enter would open the editor for keys that don't apply; show why instead.
  [HarmonyPrefix]
  private static bool Prefix(PauseSettingButton setting, SettingsMenu.Interaction action)
  {
    if (action != SettingsMenu.Interaction.Activate || !KeyLimiterMenuRow.Owns(setting))
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
    if (action == SettingsMenu.Interaction.Refresh && GameKeyLimiter.Active)
      setting.valueLabel.text = KeyLimiterMenuRow.Label;
  }
}

// The game's "Press Enter to add/remove keys" doesn't hold while ImplDmNote's keys apply.
[HarmonyPatch(typeof(PauseSettingButton), nameof(PauseSettingButton.GetDescriptionText))]
[PatchGroup("KeyLimiter")]
internal static class KeyLimiterMenuDescriptionPatch
{
  [HarmonyPrefix]
  private static bool Prefix(PauseSettingButton __instance, ref string __result)
  {
    if (!KeyLimiterMenuRow.Owns(__instance))
      return true;
    __result = KeyLimiterMenuRow.Description;
    return false;
  }
}

[HarmonyPatch(typeof(PauseSettingButton), nameof(PauseSettingButton.SetFocus))]
[PatchGroup("KeyLimiter")]
internal static class KeyLimiterMenuFocusPatch
{
  [HarmonyPostfix]
  private static void Postfix(PauseSettingButton __instance, bool focus) =>
    KeyLimiterMenuRow.TrackFocus(__instance, focus);
}
