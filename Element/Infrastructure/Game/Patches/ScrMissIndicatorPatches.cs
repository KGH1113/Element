using HarmonyLib;

namespace Element.Infrastructure.Game.Patches;

/// <summary>scrMissIndicator: miss marks, hidden in record mode.</summary>
internal static class ScrMissIndicatorPatches
{
  [HarmonyPatch(typeof(scrMissIndicator), "Awake")]
  [PatchGroup(PatchGroupNames.Recording)]
  private static class Awake
  {
    [HarmonyPostfix]
    private static void Postfix(scrMissIndicator __instance) => GameHooks.Recording?.HideMissIndicator(__instance);
  }
}
