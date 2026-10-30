using HarmonyLib;

namespace Element.Infrastructure.Game.Status;

[HarmonyPatch(typeof(scrMarginTracker), nameof(scrMarginTracker.CalculatePercentAcc))]
internal static class MarginTrackerAccuracyPatch
{
  [HarmonyPostfix]
  private static void Postfix()
  {
    Main.Instance?.Runtime?.OnAccuracyChanged();
  }
}
