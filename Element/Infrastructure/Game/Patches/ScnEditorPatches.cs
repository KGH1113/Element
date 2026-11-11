using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using Element.Application.Input;
using Element.Infrastructure.Game.Editor;
using HarmonyLib;
using UnityEngine;

namespace Element.Infrastructure.Game.Patches;

/// <summary>
/// scnEditor: the level editor's update, its transforms, and leaving play.
/// Update, in the order the game runs them:
/// 1. prefixes, both at normal priority, the input gate's group applied first: the
///    input gate skips the editor's update while the settings own the mouse (a prefix
///    that returns bool); the trackpad then reads this frame's gestures (it returns
///    nothing, so it runs even when the update is skipped);
/// 2. the trackpad's transpiler takes the editor's scroll;
/// 3. postfixes: the trackpad at normal priority moves the camera, then the editor
///    tweaks at the last priority redraw the selection's numbers.
/// SwitchToEditMode: effects keep the visual preset through the switch (prefix and
/// finalizer); record mode hides the editor UI again afterwards (postfix).
/// </summary>
internal static class ScnEditorPatches
{
  [HarmonyPatch(typeof(scnEditor), "Update")]
  [PatchGroup(PatchGroupNames.Input)]
  private static class UpdateForInputGate
  {
    [HarmonyPrefix]
    private static bool Prefix()
    {
      // The editor reads raw Unity keyboard/mouse input throughout Update,
      // including Escape before HandleKeyboardActions / HandleMouseActions.
      return !InputGate.BlocksPointerInput;
    }
  }

  [HarmonyPatch]
  [PatchGroup(PatchGroupNames.Trackpad)]
  private static class UpdateForTrackpad
  {
    private static bool Prepare() => TrackpadPatches.OnMac;

    private static MethodBase TargetMethod() => AccessTools.Method(typeof(scnEditor), "Update");

    [HarmonyPrefix]
    private static void Prefix() => GameHooks.Trackpad?.BeforeEditorUpdate();

    [HarmonyPostfix]
    private static void Postfix(scnEditor __instance) => GameHooks.Trackpad?.AfterEditorUpdate(__instance);

    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) =>
      TrackpadPatches.ReplaceOnce(
        instructions,
        AccessTools.PropertyGetter(typeof(RDInput), nameof(RDInput.mouseScrollDelta)),
        AccessTools.Method(typeof(UpdateForTrackpad), nameof(EditorScroll)),
        "Editor scroll hook changed; expected exactly one call"
      );

    private static Vector2 EditorScroll()
    {
      EditorTrackpadController trackpad = GameHooks.Trackpad;
      return trackpad != null ? trackpad.EditorScroll() : RDInput.mouseScrollDelta;
    }
  }

  [HarmonyPatch]
  [PatchGroup(PatchGroupNames.EditorTweaks + "Update")]
  private static class UpdateForEditorTweaks
  {
    private static MethodBase TargetMethod() => AccessTools.Method(typeof(scnEditor), "Update");

    [HarmonyPostfix]
    [HarmonyPriority(Priority.Last)]
    private static void Postfix(scnEditor __instance) => GameHooks.Tweaks?.AfterEditorUpdate(__instance);
  }

  [HarmonyPatch]
  [PatchGroup(PatchGroupNames.Trackpad)]
  private static class ZoomCamera
  {
    private static bool Prepare() => TrackpadPatches.OnMac;

    private static MethodBase TargetMethod() => AccessTools.Method(typeof(scnEditor), nameof(scnEditor.ZoomCamera));

    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) =>
      TrackpadPatches.ReplaceOnce(
        instructions,
        AccessTools.PropertyGetter(typeof(UnityEngine.Input), nameof(UnityEngine.Input.mousePosition)),
        AccessTools.Method(typeof(ZoomCamera), nameof(ZoomPointer)),
        "Editor zoom hook changed; expected exactly one pointer read"
      );

    private static Vector3 ZoomPointer() => GameHooks.Trackpad?.ZoomPointer ?? UnityEngine.Input.mousePosition;
  }

  [HarmonyPatch]
  [PatchGroup(PatchGroupNames.EditorTweaks + "OnSelectedFloorChange")]
  private static class OnSelectedFloorChange
  {
    private static MethodBase TargetMethod() => AccessTools.Method(typeof(scnEditor), "OnSelectedFloorChange");

    [HarmonyPostfix]
    [HarmonyPriority(Priority.Last)]
    private static void Postfix() => GameHooks.Tweaks?.InvalidateEditor();
  }

  [HarmonyPatch]
  [PatchGroup(PatchGroupNames.EditorTweaks + "RemakePath")]
  private static class RemakePath
  {
    private static MethodBase TargetMethod() => AccessTools.Method(typeof(scnEditor), "RemakePath");

    [HarmonyPostfix]
    [HarmonyPriority(Priority.Last)]
    private static void Postfix() => GameHooks.Tweaks?.InvalidateEditor();
  }

  [HarmonyPatch]
  [PatchGroup(PatchGroupNames.EditorTweaks + "DrawFloorNums")]
  private static class DrawFloorNums
  {
    private static MethodBase TargetMethod() => AccessTools.Method(typeof(scnEditor), "DrawFloorNums");

    [HarmonyPostfix]
    [HarmonyPriority(Priority.Last)]
    private static void Postfix() => GameHooks.Tweaks?.InvalidateEditor();
  }

  [HarmonyPatch]
  [PatchGroup(PatchGroupNames.EditorTweaks + "FlipFloor")]
  private static class FlipFloor
  {
    private static MethodBase TargetMethod() => AccessTools.Method(typeof(scnEditor), "FlipFloor");

    [HarmonyPrefix]
    [HarmonyPriority(Priority.Last)]
    private static void Prefix(
      scnEditor __instance,
      scrFloor floor,
      MethodBase __originalMethod,
      object[] __args,
      out EditorTweaksController.Mutation __state
    ) => __state = BeforeTransform(__instance, floor, __originalMethod, __args);

    [HarmonyPostfix]
    [HarmonyPriority(Priority.Last)]
    private static void Postfix(
      scnEditor __instance,
      bool remakePath,
      bool __runOriginal,
      EditorTweaksController.Mutation __state
    ) => GameHooks.Tweaks?.AfterEditorTransform(__instance, remakePath, __runOriginal, __state);

    [HarmonyFinalizer]
    [HarmonyPriority(Priority.Last)]
    private static Exception Finalizer(Exception __exception, EditorTweaksController.Mutation __state) =>
      FinishTransform(__exception, __state);
  }

  [HarmonyPatch]
  [PatchGroup(PatchGroupNames.EditorTweaks + "RotateFloor")]
  private static class RotateFloor
  {
    private static MethodBase TargetMethod() => AccessTools.Method(typeof(scnEditor), "RotateFloor");

    [HarmonyPrefix]
    [HarmonyPriority(Priority.Last)]
    private static void Prefix(
      scnEditor __instance,
      scrFloor floor,
      MethodBase __originalMethod,
      object[] __args,
      out EditorTweaksController.Mutation __state
    ) => __state = BeforeTransform(__instance, floor, __originalMethod, __args);

    [HarmonyPostfix]
    [HarmonyPriority(Priority.Last)]
    private static void Postfix(
      scnEditor __instance,
      bool remakePath,
      bool __runOriginal,
      EditorTweaksController.Mutation __state
    ) => GameHooks.Tweaks?.AfterEditorTransform(__instance, remakePath, __runOriginal, __state);

    [HarmonyFinalizer]
    [HarmonyPriority(Priority.Last)]
    private static Exception Finalizer(Exception __exception, EditorTweaksController.Mutation __state) =>
      FinishTransform(__exception, __state);
  }

  [HarmonyPatch]
  [PatchGroup(PatchGroupNames.EditorTweaks + "RotateFloor180")]
  private static class RotateFloor180
  {
    private static MethodBase TargetMethod() => AccessTools.Method(typeof(scnEditor), "RotateFloor180");

    [HarmonyPrefix]
    [HarmonyPriority(Priority.Last)]
    private static void Prefix(
      scnEditor __instance,
      scrFloor floor,
      MethodBase __originalMethod,
      object[] __args,
      out EditorTweaksController.Mutation __state
    ) => __state = BeforeTransform(__instance, floor, __originalMethod, __args);

    [HarmonyPostfix]
    [HarmonyPriority(Priority.Last)]
    private static void Postfix(
      scnEditor __instance,
      bool remakePath,
      bool __runOriginal,
      EditorTweaksController.Mutation __state
    ) => GameHooks.Tweaks?.AfterEditorTransform(__instance, remakePath, __runOriginal, __state);

    [HarmonyFinalizer]
    [HarmonyPriority(Priority.Last)]
    private static Exception Finalizer(Exception __exception, EditorTweaksController.Mutation __state) =>
      FinishTransform(__exception, __state);
  }

  [HarmonyPatch(typeof(scnEditor), "ResetScene")]
  [PatchGroup(PatchGroupNames.Lifecycle)]
  private static class ResetScene
  {
    [HarmonyPostfix]
    private static void Postfix() => GameHooks.Gameplay?.HideGameplay();
  }

  [HarmonyPatch(typeof(scnEditor), "SwitchToEditMode")]
  [PatchGroup(PatchGroupNames.Effects)]
  private static class SwitchToEditModeForEffects
  {
    [HarmonyPrefix]
    private static void Prefix(out IDisposable __state) => __state = GameHooks.Effects?.BeginEditorReturn();

    [HarmonyFinalizer]
    private static void Finalizer(IDisposable __state) => __state?.Dispose();
  }

  [HarmonyPatch(typeof(scnEditor), "SwitchToEditMode")]
  [PatchGroup(PatchGroupNames.Recording)]
  private static class SwitchToEditModeForRecording
  {
    [HarmonyPostfix]
    private static void Postfix() => GameHooks.Recording?.ReapplyUi();
  }

  private static EditorTweaksController.Mutation BeforeTransform(
    scnEditor editor,
    scrFloor floor,
    MethodBase original,
    object[] args
  ) => GameHooks.Tweaks?.BeginEditorTransform(editor, floor, original.Name, args);

  private static Exception FinishTransform(Exception exception, EditorTweaksController.Mutation mutation)
  {
    mutation?.Dispose();
    return exception;
  }
}
