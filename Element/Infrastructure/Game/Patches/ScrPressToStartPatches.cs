using HarmonyLib;

namespace Element.Infrastructure.Game.Patches;

/// <summary>scrPressToStart: "press any key" before a play, where scnGame.Play is not called.</summary>
internal static class ScrPressToStartPatches
{
  [HarmonyPatch(typeof(scrPressToStart), nameof(scrPressToStart.ShowText))]
  [PatchGroup(PatchGroupNames.Lifecycle)]
  private static class ShowText
  {
    [HarmonyPostfix]
    private static void Postfix()
    {
      if (!GCS.practiceMode && scnGame.instance != null)
        return;

      scrController controller = scrController.instance;
      GameHooks.Gameplay?.BeginGameplay(controller != null ? controller.currentSeqID : 0);
    }
  }
}
