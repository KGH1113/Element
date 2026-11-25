using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using Element.Application.Input;
using Element.Infrastructure.Game.Input;
using HarmonyLib;
using UnityEngine;

namespace Element.Infrastructure.Game.Patches;

/// <summary>scrController: input each frame, the level's end and rewind, and leaving the scene.</summary>
internal static class ScrControllerPatches
{
  [HarmonyPatch(typeof(scrController), nameof(scrController.UpdateInput))]
  [PatchGroup(PatchGroupNames.Input)]
  private static class UpdateInput
  {
    [HarmonyPrefix]
    private static void Prefix()
    {
      if (InputGate.BlocksGameInput)
        GameAsyncInput.Clear();
    }
  }

  [HarmonyPatch(typeof(scrController), "Update")]
  [PatchGroup(PatchGroupNames.Input)]
  private static class Update
  {
    private static readonly MethodInfo GetKeyDown = AccessTools.Method(
      typeof(UnityEngine.Input),
      nameof(UnityEngine.Input.GetKeyDown),
      new[] { typeof(KeyCode) }
    );
    private static readonly MethodInfo GetKey = AccessTools.Method(
      typeof(UnityEngine.Input),
      nameof(UnityEngine.Input.GetKey),
      new[] { typeof(KeyCode) }
    );
    private static readonly MethodInfo ReadKeyDownMethod = AccessTools.Method(typeof(Update), nameof(ReadKeyDown));
    private static readonly MethodInfo ReadKeyMethod = AccessTools.Method(typeof(Update), nameof(ReadKey));

    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
      // Only redirect the controller's raw shortcuts. Patching Unity Input
      // globally would also disable the retained settings UI's EventSystem.
      foreach (CodeInstruction instruction in instructions)
      {
        if (instruction.Calls(GetKeyDown))
        {
          instruction.opcode = OpCodes.Call;
          instruction.operand = ReadKeyDownMethod;
        }
        else if (instruction.Calls(GetKey))
        {
          instruction.opcode = OpCodes.Call;
          instruction.operand = ReadKeyMethod;
        }
        yield return instruction;
      }
    }

    private static bool ReadKeyDown(KeyCode key)
    {
      return !InputGate.BlocksPointerInput && UnityEngine.Input.GetKeyDown(key);
    }

    private static bool ReadKey(KeyCode key)
    {
      return !InputGate.BlocksPointerInput && UnityEngine.Input.GetKey(key);
    }
  }

  [HarmonyPatch(typeof(scrController), "OnLandOnPortal")]
  [PatchGroup(PatchGroupNames.Recording)]
  private static class OnLandOnPortal
  {
    [HarmonyPrefix]
    private static void Prefix()
    {
      IRecordingHooks recording = GameHooks.Recording;
      if (recording != null && recording.IsRecording)
        recording.SuppressNextFlash = true;
    }

    [HarmonyPostfix]
    private static void Postfix() => GameHooks.Recording?.ReapplyUi();
  }

  [HarmonyPatch(typeof(scrController), "UpdateErrorMeterVisibility")]
  [PatchGroup(PatchGroupNames.Recording)]
  private static class UpdateErrorMeterVisibility
  {
    [HarmonyPostfix]
    private static void Postfix() => GameHooks.Recording?.HideErrorMeter();
  }

  [HarmonyPatch(typeof(scrController), "Awake_Rewind")]
  [PatchGroup(PatchGroupNames.Recording)]
  private static class AwakeRewind
  {
    [HarmonyPostfix]
    private static void Postfix() => GameHooks.Recording?.ReapplyUi();
  }

  [HarmonyPatch(typeof(scrController), nameof(scrController.StartLoadingScene))]
  [PatchGroup(PatchGroupNames.Lifecycle)]
  private static class StartLoadingScene
  {
    [HarmonyPostfix]
    private static void Postfix() => GameHooks.Gameplay?.HideGameplay();
  }
}
