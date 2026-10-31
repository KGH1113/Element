using HarmonyLib;

namespace Element.Infrastructure.Game.Judgement;

[HarmonyPatch(typeof(scrMarginTracker), nameof(scrMarginTracker.AddHit))]
internal static class MarginTrackerAddHitPatch
{
  [HarmonyPostfix]
  private static void Postfix(scrMarginTracker __instance, HitMargin hit)
  {
    Main.Instance?.Runtime?.OnHit(__instance, hit);
  }
}

[HarmonyPatch(typeof(scrMarginTracker), nameof(scrMarginTracker.Reset))]
internal static class MarginTrackerResetPatch
{
  [HarmonyPostfix]
  private static void Postfix(scrMarginTracker __instance)
  {
    Main.Instance?.Runtime?.OnTrackerReset(__instance);
  }
}

[HarmonyPatch(typeof(scrMarginTracker), nameof(scrMarginTracker.RevertToLastCheckpoint))]
internal static class MarginTrackerCheckpointPatch
{
  [HarmonyPostfix]
  private static void Postfix(scrMarginTracker __instance)
  {
    Main.Instance?.Runtime?.OnTrackerReset(__instance);
  }
}

[HarmonyPatch(typeof(scrPlayer), nameof(scrPlayer.Die))]
internal static class PlayerDiePatch
{
  [HarmonyPostfix]
  private static void Postfix(scrPlayer __instance)
  {
    // No-fail, safe tiles and spare lives make Die return early while the player keeps playing.
    if (__instance.alive)
      return;

    Main.Instance?.Runtime?.OnPlayerDied(__instance.marginTracker);
  }
}
