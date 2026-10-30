using HarmonyLib;

namespace Element.Infrastructure.Game.Judgement;

[HarmonyPatch(typeof(scrHitTextManager), "ShowHitText")]
internal static class HitTextVisibilityPatch
{
  [HarmonyPrefix]
  private static bool Prefix(HitMargin hitMargin)
  {
    return Main.Instance?.Runtime?.ShouldShowHitText(hitMargin) ?? true;
  }
}
