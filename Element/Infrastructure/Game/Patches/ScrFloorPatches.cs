using System.Reflection;
using HarmonyLib;

namespace Element.Infrastructure.Game.Patches;

/// <summary>scrFloor: floors coming into and out of the editor's view, which redraw the selection's numbers.</summary>
internal static class ScrFloorPatches
{
  [HarmonyPatch]
  [PatchGroup(PatchGroupNames.EditorTweaks + "OnBecameVisible")]
  private static class OnBecameVisible
  {
    private static MethodBase TargetMethod() => AccessTools.Method(typeof(scrFloor), "OnBecameVisible");

    [HarmonyPostfix]
    [HarmonyPriority(Priority.Last)]
    private static void Postfix(scrFloor __instance) => GameHooks.EditorTweaks?.OnFloorVisibilityChanged(__instance);
  }

  [HarmonyPatch]
  [PatchGroup(PatchGroupNames.EditorTweaks + "OnBecameInvisible")]
  private static class OnBecameInvisible
  {
    private static MethodBase TargetMethod() => AccessTools.Method(typeof(scrFloor), "OnBecameInvisible");

    [HarmonyPostfix]
    [HarmonyPriority(Priority.Last)]
    private static void Postfix(scrFloor __instance) => GameHooks.EditorTweaks?.OnFloorVisibilityChanged(__instance);
  }
}
