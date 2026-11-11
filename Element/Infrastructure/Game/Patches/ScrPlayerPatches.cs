using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using Element.Application.Input;
using HarmonyLib;
using UnityEngine;

namespace Element.Infrastructure.Game.Patches;

/// <summary>
/// scrPlayer: the key presses that hit a tile, and the player's death.
/// CountValidKeysPressed: the input gate's prefix may answer 0 and skip the game's
/// count; the chatter blocker's postfix runs either way and leaves a 0 alone.
/// </summary>
internal static class ScrPlayerPatches
{
  [HarmonyPatch(typeof(scrPlayer), nameof(scrPlayer.CountValidKeysPressed))]
  [PatchGroup(PatchGroupNames.Input)]
  private static class CountValidKeysPressedForInputGate
  {
    [HarmonyPrefix]
    private static bool Prefix(ref int __result)
    {
      if (!InputGate.BlocksGameInput)
        return true;

      __result = 0;
      return false;
    }
  }

  [HarmonyPatch(typeof(scrPlayer), nameof(scrPlayer.CountValidKeysPressed))]
  [PatchGroup(PatchGroupNames.ChatterBlocker)]
  private static class CountValidKeysPressedForChatterBlocker
  {
    [HarmonyPostfix]
    private static void Postfix(ref int __result) => GameHooks.Chatter?.RemoveChatter(ref __result);
  }

  [HarmonyPatch(typeof(scrPlayer), nameof(scrPlayer.ValidInputWasTriggered))]
  [PatchGroup(PatchGroupNames.Input)]
  private static class ValidInputWasTriggered
  {
    private static readonly MethodInfo GetKeyDown = AccessTools.Method(
      typeof(UnityEngine.Input),
      nameof(UnityEngine.Input.GetKeyDown),
      new[] { typeof(KeyCode) }
    );
    private static readonly MethodInfo ReadKeyDownMethod = AccessTools.Method(
      typeof(ValidInputWasTriggered),
      nameof(ReadKeyDown)
    );

    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
      foreach (CodeInstruction instruction in instructions)
      {
        if (instruction.Calls(GetKeyDown))
        {
          instruction.opcode = OpCodes.Call;
          instruction.operand = ReadKeyDownMethod;
        }
        yield return instruction;
      }
    }

    private static bool ReadKeyDown(KeyCode key)
    {
      // ADOFAI normally rejects all input on a mouse-down over UI, including
      // a simultaneous keyboard note. A captured drag must not reject that note.
      if (InputGate.BlocksPointerInput && key >= KeyCode.Mouse0 && key <= KeyCode.Mouse6)
        return false;
      return UnityEngine.Input.GetKeyDown(key);
    }
  }

  [HarmonyPatch(typeof(scrPlayer), nameof(scrPlayer.Die))]
  [PatchGroup(PatchGroupNames.Judgement)]
  private static class Die
  {
    [HarmonyPostfix]
    private static void Postfix(scrPlayer __instance)
    {
      // No-fail, safe tiles and spare lives make Die return early while the player keeps playing.
      if (__instance.alive)
        return;

      GameHooks.Gameplay?.OnPlayerDied(__instance.marginTracker);
    }
  }
}
