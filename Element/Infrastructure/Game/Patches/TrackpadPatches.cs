using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace Element.Infrastructure.Game.Patches;

/// <summary>What the macOS trackpad's patches share: they apply on macOS only, and their transpilers check what they replace.</summary>
internal static class TrackpadPatches
{
  public static bool OnMac => UnityEngine.Application.platform == RuntimePlatform.OSXPlayer;

  /// <summary>Replaces the one call of <paramref name="original"/>; throws when the game has none or several.</summary>
  public static IEnumerable<CodeInstruction> ReplaceOnce(
    IEnumerable<CodeInstruction> instructions,
    MethodInfo original,
    MethodInfo replacement,
    string changed
  )
  {
    int matches = 0;
    var result = new List<CodeInstruction>();
    foreach (CodeInstruction instruction in instructions)
    {
      if (instruction.Calls(original))
      {
        instruction.opcode = System.Reflection.Emit.OpCodes.Call;
        instruction.operand = replacement;
        matches++;
      }
      result.Add(instruction);
    }
    if (matches != 1)
      throw new InvalidOperationException(changed);
    return result;
  }
}
