using System;
using System.Reflection;
using Element.Infrastructure.Game.Planets;
using HarmonyLib;
using UnityEngine;

namespace Element.Infrastructure.Game.Patches;

/// <summary>
/// PlanetRenderer: the planets' colors, which planet appearance overrides. Each method
/// is its own group, so a method a game version lacks turns off only its own hook.
/// </summary>
internal static class PlanetRendererPatches
{
  [HarmonyPatch]
  [PatchGroup(PatchGroupNames.PlanetAppearance + "SetPlanetColor")]
  private static class SetPlanetColor
  {
    private static MethodBase TargetMethod() => AccessTools.Method(typeof(PlanetRenderer), "SetPlanetColor");

    [HarmonyPrefix]
    [HarmonyPriority(Priority.Last)]
    private static void Prefix(PlanetRenderer __instance, MethodBase __originalMethod, ref Color color) =>
      GameHooks.Tweaks?.BeforePlanetColor(__instance, __originalMethod.Name, ref color);

    [HarmonyPostfix]
    [HarmonyPriority(Priority.Last)]
    private static void Postfix(PlanetRenderer __instance) => GameHooks.Tweaks?.AfterPlanetBodyColor(__instance);
  }

  [HarmonyPatch]
  [PatchGroup(PatchGroupNames.PlanetAppearance + "SetCoreColor")]
  private static class SetCoreColor
  {
    private static MethodBase TargetMethod() => AccessTools.Method(typeof(PlanetRenderer), "SetCoreColor");

    [HarmonyPrefix]
    [HarmonyPriority(Priority.Last)]
    private static void Prefix(PlanetRenderer __instance, MethodBase __originalMethod, ref Color color) =>
      GameHooks.Tweaks?.BeforePlanetColor(__instance, __originalMethod.Name, ref color);
  }

  [HarmonyPatch]
  [PatchGroup(PatchGroupNames.PlanetAppearance + "SetTailColor")]
  private static class SetTailColor
  {
    private static MethodBase TargetMethod() => AccessTools.Method(typeof(PlanetRenderer), "SetTailColor");

    [HarmonyPrefix]
    [HarmonyPriority(Priority.Last)]
    private static void Prefix(PlanetRenderer __instance, MethodBase __originalMethod, ref Color color) =>
      GameHooks.Tweaks?.BeforePlanetColor(__instance, __originalMethod.Name, ref color);
  }

  [HarmonyPatch]
  [PatchGroup(PatchGroupNames.PlanetAppearance + "SetRingColor")]
  private static class SetRingColor
  {
    private static MethodBase TargetMethod() => AccessTools.Method(typeof(PlanetRenderer), "SetRingColor");

    [HarmonyPrefix]
    [HarmonyPriority(Priority.Last)]
    private static void Prefix(PlanetRenderer __instance, MethodBase __originalMethod, ref Color color) =>
      GameHooks.Tweaks?.BeforePlanetColor(__instance, __originalMethod.Name, ref color);
  }

  [HarmonyPatch]
  [PatchGroup(PatchGroupNames.PlanetAppearance + "SetFaceColor")]
  private static class SetFaceColor
  {
    private static MethodBase TargetMethod() => AccessTools.Method(typeof(PlanetRenderer), "SetFaceColor");

    [HarmonyPrefix]
    [HarmonyPriority(Priority.Last)]
    private static void Prefix(PlanetRenderer __instance, MethodBase __originalMethod, ref Color color) =>
      GameHooks.Tweaks?.BeforePlanetColor(__instance, __originalMethod.Name, ref color);
  }

  [HarmonyPatch]
  [PatchGroup(PatchGroupNames.PlanetAppearance + "SetColor")]
  private static class SetColor
  {
    private static MethodBase TargetMethod() => AccessTools.Method(typeof(PlanetRenderer), "SetColor");

    [HarmonyPrefix]
    [HarmonyPriority(Priority.Last)]
    private static void Prefix(PlanetRenderer __instance, out PlanetAppearanceController.PlanetState __state) =>
      __state = GameHooks.Tweaks?.BeginPlanetAppearance(__instance);

    [HarmonyFinalizer]
    [HarmonyPriority(Priority.Last)]
    private static Exception Finalizer(Exception __exception, PlanetAppearanceController.PlanetState __state)
    {
      GameHooks.Tweaks?.FinishPlanetAppearance(__state);
      return __exception;
    }
  }

  [HarmonyPatch]
  [PatchGroup(PatchGroupNames.PlanetAppearance + "LoadPlanetColor")]
  private static class LoadPlanetColor
  {
    private static MethodBase TargetMethod() => AccessTools.Method(typeof(PlanetRenderer), "LoadPlanetColor");

    [HarmonyPrefix]
    [HarmonyPriority(Priority.Last)]
    private static void Prefix(PlanetRenderer __instance, out PlanetAppearanceController.PlanetState __state) =>
      __state = GameHooks.Tweaks?.BeginPlanetAppearance(__instance);

    [HarmonyFinalizer]
    [HarmonyPriority(Priority.Last)]
    private static Exception Finalizer(Exception __exception, PlanetAppearanceController.PlanetState __state)
    {
      GameHooks.Tweaks?.FinishPlanetAppearance(__state);
      return __exception;
    }
  }

  [HarmonyPatch]
  [PatchGroup(PatchGroupNames.PlanetAppearance + "EnableCustomColor")]
  private static class EnableCustomColor
  {
    private static MethodBase TargetMethod() => AccessTools.Method(typeof(PlanetRenderer), "EnableCustomColor");

    [HarmonyPrefix]
    [HarmonyPriority(Priority.Last)]
    private static void Prefix(PlanetRenderer __instance, out PlanetAppearanceController.PlanetState __state) =>
      __state = GameHooks.Tweaks?.BeginPlanetAppearance(__instance);

    [HarmonyFinalizer]
    [HarmonyPriority(Priority.Last)]
    private static Exception Finalizer(Exception __exception, PlanetAppearanceController.PlanetState __state)
    {
      GameHooks.Tweaks?.FinishPlanetAppearance(__state);
      return __exception;
    }
  }

  [HarmonyPatch]
  [PatchGroup(PatchGroupNames.PlanetAppearance + "EnableDefaultFireAndIceColor")]
  private static class EnableDefaultFireAndIceColor
  {
    private static MethodBase TargetMethod() =>
      AccessTools.Method(typeof(PlanetRenderer), "EnableDefaultFireAndIceColor");

    [HarmonyPrefix]
    [HarmonyPriority(Priority.Last)]
    private static void Prefix(PlanetRenderer __instance, out PlanetAppearanceController.PlanetState __state) =>
      __state = GameHooks.Tweaks?.BeginPlanetAppearance(__instance);

    [HarmonyFinalizer]
    [HarmonyPriority(Priority.Last)]
    private static Exception Finalizer(Exception __exception, PlanetAppearanceController.PlanetState __state)
    {
      GameHooks.Tweaks?.FinishPlanetAppearance(__state);
      return __exception;
    }
  }

  [HarmonyPatch]
  [PatchGroup(PatchGroupNames.PlanetAppearance + "ToggleSpecialPlanet")]
  private static class ToggleSpecialPlanet
  {
    private static MethodBase TargetMethod() => AccessTools.Method(typeof(PlanetRenderer), "ToggleSpecialPlanet");

    [HarmonyPrefix]
    [HarmonyPriority(Priority.Last)]
    private static void Prefix(PlanetRenderer __instance, out PlanetAppearanceController.PlanetState __state) =>
      __state = GameHooks.Tweaks?.BeginPlanetAppearance(__instance);

    [HarmonyFinalizer]
    [HarmonyPriority(Priority.Last)]
    private static Exception Finalizer(Exception __exception, PlanetAppearanceController.PlanetState __state)
    {
      GameHooks.Tweaks?.FinishPlanetAppearance(__state);
      return __exception;
    }
  }

  [HarmonyPatch]
  [PatchGroup(PatchGroupNames.PlanetAppearance + "LateUpdate")]
  private static class LateUpdate
  {
    private static MethodBase TargetMethod() => AccessTools.Method(typeof(PlanetRenderer), "LateUpdate");

    [HarmonyPostfix]
    [HarmonyPriority(Priority.Last)]
    private static void Postfix(PlanetRenderer __instance) => GameHooks.Tweaks?.AfterPlanetFrame(__instance);
  }
}
