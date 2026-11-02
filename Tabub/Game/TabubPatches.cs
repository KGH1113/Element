using HarmonyLib;

namespace Tabub.Game;

// The same moments Element itself follows: a level starts, a hit lands, the
// planet goes back to a checkpoint, the level is left.

[HarmonyPatch(typeof(scnGame), nameof(scnGame.Play))]
internal static class PlayPatch
{
  private static void Postfix() => TabubSession.Begin();
}

// Practice and the level editor start here instead.
[HarmonyPatch(typeof(scrPressToStart), nameof(scrPressToStart.ShowText))]
internal static class PressToStartPatch
{
  private static void Postfix() => TabubSession.Begin();
}

[HarmonyPatch(typeof(scrMarginTracker), nameof(scrMarginTracker.AddHit))]
internal static class HitPatch
{
  private static void Postfix() => TabubSession.Advance();
}

[HarmonyPatch(typeof(scrMarginTracker), nameof(scrMarginTracker.RevertToLastCheckpoint))]
internal static class CheckpointPatch
{
  private static void Postfix() => TabubSession.Advance();
}

[HarmonyPatch(typeof(scrUIController), nameof(scrUIController.WipeToBlack))]
internal static class LeavePatch
{
  private static void Postfix() => TabubSession.End();
}

[HarmonyPatch(typeof(scrController), nameof(scrController.StartLoadingScene))]
internal static class LoadingScenePatch
{
  private static void Postfix() => TabubSession.End();
}
