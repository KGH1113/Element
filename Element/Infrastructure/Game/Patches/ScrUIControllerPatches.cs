using HarmonyLib;

namespace Element.Infrastructure.Game.Patches;

/// <summary>scrUIController: the wipe to black that ends a play.</summary>
internal static class ScrUIControllerPatches
{
  [HarmonyPatch(typeof(scrUIController), nameof(scrUIController.WipeToBlack))]
  [PatchGroup(PatchGroupNames.Lifecycle)]
  private static class WipeToBlack
  {
    [HarmonyPostfix]
    private static void Postfix() => GameHooks.Gameplay?.HideGameplay();
  }
}
