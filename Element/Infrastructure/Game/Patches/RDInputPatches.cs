using System.Collections.Generic;
using Element.Application.Input;
using HarmonyLib;
using UnityEngine;

namespace Element.Infrastructure.Game.Patches;

/// <summary>RDInput: the game's own input reads, held back while the input gate is closed.</summary>
internal static class RDInputPatches
{
  [HarmonyPatch(typeof(RDInput), nameof(RDInput.GetMain))]
  [PatchGroup(PatchGroupNames.Input)]
  private static class GetMainPresses
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

  [HarmonyPatch(typeof(RDInput), nameof(RDInput.GetState))]
  [PatchGroup(PatchGroupNames.Input)]
  private static class GetState
  {
    [HarmonyPrefix]
    private static bool Prefix(ref bool __result)
    {
      if (!InputGate.BlocksPointerInput)
        return true;

      __result = false;
      return false;
    }
  }

  [HarmonyPatch(typeof(RDInput), nameof(RDInput.GetStateKeys))]
  [PatchGroup(PatchGroupNames.Input)]
  private static class GetStateKeys
  {
    [HarmonyPrefix]
    private static bool Prefix(List<AnyKeyCode> keys)
    {
      if (!InputGate.BlocksGameInput)
        return true;

      keys.Clear();
      return false;
    }

    [HarmonyPostfix]
    private static void Postfix(List<AnyKeyCode> keys)
    {
      if (!InputGate.BlocksPointerInput)
        return;

      // GetStateKeys reads each input's cached list, even when Main returned zero.
      // Preserve keyboard rhythm input while editing an overlay with the mouse.
      for (int index = keys.Count - 1; index >= 0; index--)
      {
        if (keys[index].value is KeyCode key && key >= KeyCode.Mouse0 && key <= KeyCode.Mouse6)
          keys.RemoveAt(index);
      }
    }
  }

  [HarmonyPatch(typeof(RDInput), "GetPosition")]
  [PatchGroup(PatchGroupNames.Input)]
  private static class GetPosition
  {
    [HarmonyPrefix]
    private static bool Prefix(ref Vector2 __result)
    {
      if (!InputGate.BlocksPointerInput)
        return true;

      __result = Vector2.zero;
      return false;
    }
  }

  [HarmonyPatch(typeof(RDInput), nameof(RDInput.mouseScrollDelta), MethodType.Getter)]
  [PatchGroup(PatchGroupNames.Input)]
  private static class MouseScrollDelta
  {
    [HarmonyPrefix]
    private static bool Prefix(ref Vector2 __result)
    {
      if (!InputGate.BlocksPointerInput)
        return true;

      __result = Vector2.zero;
      return false;
    }
  }
}
