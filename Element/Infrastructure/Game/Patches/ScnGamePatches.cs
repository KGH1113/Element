using System;
using System.Collections.Generic;
using System.Reflection;
using ADOFAI;
using HarmonyLib;

namespace Element.Infrastructure.Game.Patches;

/// <summary>
/// scnGame: loading, building and playing a level. Rendering gets a temporary filtered
/// view; finalizers restore the exact source reference even if the game or another
/// patch throws. Saving and undo use the originals.
/// Play: effects apply the pending preset before the level starts (prefix and
/// finalizer); the attempt counter and the overlay start after it (postfix).
/// </summary>
internal static class ScnGamePatches
{
  [HarmonyPatch]
  [PatchGroup(PatchGroupNames.Effects)]
  private static class RenderScope
  {
    private static IEnumerable<MethodBase> TargetMethods()
    {
      string[] methods =
      {
        "ReloadAssets",
        "RemakePath",
        "UpdateDecorationObjects",
        "UpdateBackgroundSprites",
        "UpdateFloorSprites",
        "SetBackground",
        "SetStartingBG",
        "UpdateVideo",
        "FinishCustomLevelLoading",
      };
      foreach (string name in methods)
        yield return AccessTools.Method(typeof(scnGame), name);
      yield return AccessTools.Method(typeof(scnGame), "ApplyEventsToFloors", new[] { typeof(List<scrFloor>) });
      yield return AccessTools.Method(typeof(scnGame), "PrepVfx", new[] { typeof(int), typeof(bool) });
    }

    [HarmonyPrefix]
    private static void Prefix(scnGame __instance, out IDisposable __state) =>
      __state = GameHooks.Effects?.BeginRender(__instance);

    [HarmonyFinalizer]
    private static void Finalizer(IDisposable __state) => __state?.Dispose();
  }

  [HarmonyPatch(
    typeof(scnGame),
    "ApplyEventsToFloors",
    new[] { typeof(List<scrFloor>), typeof(LevelData), typeof(scrLevelMaker), typeof(List<LevelEvent>) }
  )]
  [PatchGroup(PatchGroupNames.Effects)]
  private static class ApplyEventsToFloors
  {
    [HarmonyPrefix]
    private static void Prefix(ref LevelData levelData, ref List<LevelEvent> events) =>
      GameHooks.Effects?.FilterFloorInput(ref levelData, ref events);
  }

  [HarmonyPatch(typeof(scnGame), "LoadLevel")]
  [PatchGroup(PatchGroupNames.Effects)]
  private static class LoadLevel
  {
    [HarmonyPrefix]
    private static void Prefix() => GameHooks.Effects?.CommitForPlayback();
  }

  [HarmonyPatch(typeof(scnGame), "ResetScene")]
  [PatchGroup(PatchGroupNames.Effects)]
  private static class ResetScene
  {
    [HarmonyPrefix]
    private static void Prefix(scnGame __instance, ref bool isResetCustomLevel, out IDisposable __state)
    {
      __state = null;
      GameHooks.Effects?.BeforeReset(__instance, ref isResetCustomLevel, out __state);
    }

    [HarmonyFinalizer]
    private static void Finalizer(IDisposable __state) => __state?.Dispose();
  }

  [HarmonyPatch(typeof(scnGame), "Play")]
  [PatchGroup(PatchGroupNames.Effects)]
  private static class PlayForEffects
  {
    [HarmonyPrefix]
    private static void Prefix(scnGame __instance, ref bool remakeFloors, out IDisposable __state)
    {
      __state = null;
      GameHooks.Effects?.BeforePlay(__instance, ref remakeFloors, out __state);
    }

    [HarmonyFinalizer]
    private static void Finalizer(IDisposable __state) => __state?.Dispose();
  }

  [HarmonyPatch(typeof(scnGame), nameof(scnGame.Play))]
  [PatchGroup(PatchGroupNames.Lifecycle)]
  private static class PlayForGameplay
  {
    [HarmonyPostfix]
    private static void Postfix(int seqID)
    {
      if (!GCS.practiceMode)
        GameHooks.Gameplay?.BeginGameplay(seqID);
    }
  }
}
