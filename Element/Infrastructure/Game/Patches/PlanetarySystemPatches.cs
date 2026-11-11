using System.Reflection;
using HarmonyLib;

namespace Element.Infrastructure.Game.Patches;

/// <summary>PlanetarySystem: the game loading or recoloring all planets, after which planet appearance applies again.</summary>
internal static class PlanetarySystemPatches
{
  [HarmonyPatch]
  [PatchGroup(PatchGroupNames.PlanetAppearance + "LoadPlanetColors")]
  private static class LoadPlanetColors
  {
    private static MethodBase TargetMethod() => AccessTools.Method(typeof(PlanetarySystem), "LoadPlanetColors");

    [HarmonyPostfix]
    [HarmonyPriority(Priority.Last)]
    private static void Postfix() => GameHooks.PlanetAppearance?.Apply();
  }

  [HarmonyPatch]
  [PatchGroup(PatchGroupNames.PlanetAppearance + "ApplyMultiplanetColors")]
  private static class ApplyMultiplanetColors
  {
    private static MethodBase TargetMethod() => AccessTools.Method(typeof(PlanetarySystem), "ApplyMultiplanetColors");

    [HarmonyPostfix]
    [HarmonyPriority(Priority.Last)]
    private static void Postfix() => GameHooks.PlanetAppearance?.Apply();
  }
}
