using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Element.Infrastructure.Game.Patches;

/// <summary>BaseInput: the scroll Unity's UI reads, which a trackpad gesture on the editor's canvas keeps.</summary>
internal static class BaseInputPatches
{
  [HarmonyPatch]
  [PatchGroup(PatchGroupNames.Trackpad)]
  private static class MouseScrollDelta
  {
    private static bool Prepare() => TrackpadPatches.OnMac;

    private static MethodBase TargetMethod() => AccessTools.PropertyGetter(typeof(BaseInput), "mouseScrollDelta");

    [HarmonyPostfix]
    private static void Postfix(ref Vector2 __result) => GameHooks.Trackpad?.FilterUiScroll(ref __result);
  }
}
