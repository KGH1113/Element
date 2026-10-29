using System;
using System.Collections.Generic;
using System.Reflection;
using ADOFAI;
using ADOFAI.LevelEditor.Controls;
using HarmonyLib;
using ImplResourcePack.Configuration.Effects;

namespace ImplResourcePack.Infrastructure.Game.Effects;

// Rendering gets a temporary filtered view. Finalizers restore the exact source
// reference even if the game or another patch throws. Saving and undo use originals.
[HarmonyPatch]
internal static class EffectRenderScopePatch
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
  private static void Prefix(scnGame __instance, out IDisposable __state)
  {
    __state = EffectsController.Active?.BeginRender(__instance);
  }

  [HarmonyFinalizer]
  private static void Finalizer(IDisposable __state) => __state?.Dispose();
}

[HarmonyPatch(
  typeof(scnGame),
  "ApplyEventsToFloors",
  new[] { typeof(List<scrFloor>), typeof(LevelData), typeof(scrLevelMaker), typeof(List<LevelEvent>) }
)]
internal static class EffectFloorInputPatch
{
  [HarmonyPrefix]
  private static void Prefix(ref LevelData levelData, ref List<LevelEvent> events)
  {
    RemovedEffects effects = EffectsController.ForRendering;
    if (EffectsController.Rendering || effects == RemovedEffects.None)
      return;
    levelData = EffectLevelFilter.Create(levelData, effects, EffectsController.CameraZoomForRendering, events);
    events = levelData.levelEvents;
  }
}

[HarmonyPatch(typeof(scnGame), "LoadLevel")]
internal static class EffectLevelLoadPatch
{
  [HarmonyPrefix]
  private static void Prefix() => EffectsController.Active?.CommitForPlayback();
}

[HarmonyPatch(typeof(scnGame), "ResetScene")]
internal static class EffectRetryPatch
{
  [HarmonyPrefix]
  private static void Prefix(scnGame __instance, ref bool isResetCustomLevel, out IDisposable __state)
  {
    EffectsController controller = EffectsController.Active;
    bool changed = controller?.CommitForPlayback() ?? false;
    __state = controller?.BeginRender(__instance);
    if (!changed)
      return;
    // A full regeneration restores effects removed by the previous preset, too.
    isResetCustomLevel = true;
    __instance.UpdateDecorationObjects();
    __instance.UpdateVideo();
  }

  [HarmonyFinalizer]
  private static void Finalizer(IDisposable __state) => __state?.Dispose();
}

[HarmonyPatch(typeof(scnEditor), "SwitchToEditMode")]
internal static class EffectReturnToEditorPatch
{
  [HarmonyPrefix]
  private static void Prefix(out IDisposable __state)
  {
    __state = EffectsController.Active?.BeginEditorReturn();
  }

  [HarmonyFinalizer]
  private static void Finalizer(IDisposable __state) => __state?.Dispose();
}

[HarmonyPatch(typeof(PropertyControl_DecorationsList), "FilterSearchResults")]
internal static class EffectEditorDecorationListPatch
{
  [HarmonyPostfix]
  private static void Postfix(List<LevelEvent> ___filteredEvents)
  {
    // Filter only the list's display entries; saving and undo still use originals.
    EffectLevelFilter.FilterEditorDecorations(
      ___filteredEvents,
      ADOBase.editor?.levelData,
      EffectsController.ForRendering
    );
  }
}

[HarmonyPatch(typeof(scnGame), "Play")]
internal static class EffectPlayPatch
{
  [HarmonyPrefix]
  private static void Prefix(scnGame __instance, ref bool remakeFloors, out IDisposable __state)
  {
    EffectsController controller = EffectsController.Active;
    bool changed = controller?.CommitForPlayback() ?? false;
    __state = controller?.BeginRender(__instance);
    if (!changed)
      return;
    remakeFloors = true;
    __instance.RemakePath();
    __instance.UpdateDecorationObjects();
    __instance.SetBackground();
    __instance.UpdateVideo();
  }

  [HarmonyFinalizer]
  private static void Finalizer(IDisposable __state) => __state?.Dispose();
}
