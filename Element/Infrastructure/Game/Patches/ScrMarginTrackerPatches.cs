using HarmonyLib;

namespace Element.Infrastructure.Game.Patches;

/// <summary>scrMarginTracker: hits, restarts and checkpoints for judgement and combo; accuracy for the status.</summary>
internal static class ScrMarginTrackerPatches
{
  [HarmonyPatch(typeof(scrMarginTracker), nameof(scrMarginTracker.AddHit))]
  [PatchGroup(PatchGroupNames.Judgement)]
  private static class AddHit
  {
    [HarmonyPostfix]
    private static void Postfix(scrMarginTracker __instance, HitMargin hit) =>
      GameHooks.Gameplay?.OnHit(__instance, hit);
  }

  [HarmonyPatch(typeof(scrMarginTracker), nameof(scrMarginTracker.Reset))]
  [PatchGroup(PatchGroupNames.Judgement)]
  private static class Reset
  {
    [HarmonyPostfix]
    private static void Postfix(scrMarginTracker __instance) => GameHooks.Gameplay?.OnTrackerReset(__instance);
  }

  [HarmonyPatch(typeof(scrMarginTracker), nameof(scrMarginTracker.RevertToLastCheckpoint))]
  [PatchGroup(PatchGroupNames.Judgement)]
  private static class RevertToLastCheckpoint
  {
    [HarmonyPostfix]
    private static void Postfix(scrMarginTracker __instance) => GameHooks.Gameplay?.OnTrackerReset(__instance);
  }

  [HarmonyPatch(typeof(scrMarginTracker), nameof(scrMarginTracker.CalculatePercentAcc))]
  [PatchGroup(PatchGroupNames.Status)]
  private static class CalculatePercentAcc
  {
    [HarmonyPostfix]
    private static void Postfix() => GameHooks.Gameplay?.OnAccuracyChanged();
  }
}
