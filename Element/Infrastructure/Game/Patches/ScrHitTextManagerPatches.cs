using HarmonyLib;

namespace Element.Infrastructure.Game.Patches;

/// <summary>scrHitTextManager: the judgment text the game shows over the planets.</summary>
internal static class ScrHitTextManagerPatches
{
  [HarmonyPatch(typeof(scrHitTextManager), "ShowHitText")]
  [PatchGroup(PatchGroupNames.Judgement)]
  private static class ShowHitText
  {
    [HarmonyPrefix]
    private static bool Prefix(HitMargin hitMargin) => GameHooks.Gameplay?.ShouldShowHitText(hitMargin) ?? true;
  }
}
