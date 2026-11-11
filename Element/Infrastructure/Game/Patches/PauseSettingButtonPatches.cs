using Element.Infrastructure.Game.Input;
using HarmonyLib;

namespace Element.Infrastructure.Game.Patches;

/// <summary>PauseSettingButton: the Key Limiter row's description and focus while ElementNote's keys apply.</summary>
internal static class PauseSettingButtonPatches
{
  // The game's "Press Enter to add/remove keys" doesn't hold while ElementNote's keys apply.
  [HarmonyPatch(typeof(PauseSettingButton), nameof(PauseSettingButton.GetDescriptionText))]
  [PatchGroup(PatchGroupNames.KeyLimiter)]
  private static class GetDescriptionText
  {
    [HarmonyPrefix]
    private static bool Prefix(PauseSettingButton __instance, ref string __result)
    {
      GameKeyLimiter limiter = GameHooks.KeyLimiter;
      if (limiter == null || !limiter.OwnsMenuRow(__instance))
        return true;
      __result = limiter.MenuDescription;
      return false;
    }
  }

  [HarmonyPatch(typeof(PauseSettingButton), nameof(PauseSettingButton.SetFocus))]
  [PatchGroup(PatchGroupNames.KeyLimiter)]
  private static class SetFocus
  {
    [HarmonyPostfix]
    private static void Postfix(PauseSettingButton __instance, bool focus) =>
      KeyLimiterMenuRow.TrackFocus(__instance, focus);
  }
}
