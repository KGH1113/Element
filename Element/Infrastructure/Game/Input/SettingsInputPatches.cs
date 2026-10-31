using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Element.Presentation.Settings;
using SkyHook;
using UnityEngine;

namespace Element.Infrastructure.Game.Input;

[HarmonyPatch(typeof(RDInput), nameof(RDInput.GetMain))]
internal static class SettingsMainInputPatch
{
  [HarmonyPrefix]
  private static bool Prefix(ref int __result)
  {
    if (!UIInteractionState.BlocksGameInput)
      return true;

    __result = 0;
    return false;
  }
}

[HarmonyPatch(typeof(RDInputType_Mouse), nameof(RDInputType_Mouse.Main))]
internal static class SettingsMouseInputPatch
{
  [HarmonyPrefix]
  private static bool Prefix(ref int __result)
  {
    if (!UIInteractionState.BlocksPointerInput)
      return true;

    __result = 0;
    return false;
  }
}

[HarmonyPatch(typeof(RDInput), nameof(RDInput.GetState))]
internal static class SettingsActionInputPatch
{
  [HarmonyPrefix]
  private static bool Prefix(ref bool __result)
  {
    if (!UIInteractionState.BlocksPointerInput)
      return true;

    __result = false;
    return false;
  }
}

[HarmonyPatch(typeof(RDInput), nameof(RDInput.GetStateKeys))]
internal static class SettingsInputKeysPatch
{
  [HarmonyPrefix]
  private static bool Prefix(List<AnyKeyCode> keys)
  {
    if (!UIInteractionState.BlocksGameInput)
      return true;

    keys.Clear();
    return false;
  }

  [HarmonyPostfix]
  private static void Postfix(List<AnyKeyCode> keys)
  {
    if (!UIInteractionState.BlocksPointerInput)
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
internal static class SettingsNavigationInputPatch
{
  [HarmonyPrefix]
  private static bool Prefix(ref Vector2 __result)
  {
    if (!UIInteractionState.BlocksPointerInput)
      return true;

    __result = Vector2.zero;
    return false;
  }
}

[HarmonyPatch(typeof(RDInput), nameof(RDInput.mouseScrollDelta), MethodType.Getter)]
internal static class SettingsScrollInputPatch
{
  [HarmonyPrefix]
  private static bool Prefix(ref Vector2 __result)
  {
    if (!UIInteractionState.BlocksPointerInput)
      return true;

    __result = Vector2.zero;
    return false;
  }
}

[HarmonyPatch(typeof(scrPlayer), nameof(scrPlayer.CountValidKeysPressed))]
internal static class SettingsPlayerInputPatch
{
  [HarmonyPrefix]
  private static bool Prefix(ref int __result)
  {
    if (!UIInteractionState.BlocksGameInput)
      return true;

    __result = 0;
    return false;
  }
}

[HarmonyPatch(typeof(scrPlayer), nameof(scrPlayer.ValidInputWasTriggered))]
internal static class SettingsPlayerPointerPatch
{
  private static readonly MethodInfo GetKeyDown = AccessTools.Method(
    typeof(UnityEngine.Input),
    nameof(UnityEngine.Input.GetKeyDown),
    new[] { typeof(KeyCode) }
  );
  private static readonly MethodInfo ReadKeyDownMethod = AccessTools.Method(
    typeof(SettingsPlayerPointerPatch),
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
    if (UIInteractionState.BlocksPointerInput && key >= KeyCode.Mouse0 && key <= KeyCode.Mouse6)
      return false;
    return UnityEngine.Input.GetKeyDown(key);
  }
}

[HarmonyPatch(typeof(scrController), nameof(scrController.UpdateInput))]
internal static class SettingsAsyncInputPatch
{
  [HarmonyPrefix]
  private static void Prefix()
  {
    if (UIInteractionState.BlocksGameInput)
      UIInteractionState.ClearAsyncInput();
  }
}

[HarmonyPatch(typeof(SkyHookManager), "HookCallback")]
internal static class SettingsHookInputPatch
{
  [HarmonyPrefix]
  [HarmonyPriority(Priority.First)]
  private static bool Prefix(SkyHookEvent ev)
  {
    // Releases must reach the game's hook state even while a modal is open.
    return ev.Type != SkyHook.EventType.KeyPressed || !UIInteractionState.BlocksAsyncKeyboard;
  }
}

[HarmonyPatch(typeof(scnEditor), "Update")]
internal static class SettingsEditorInputPatch
{
  [HarmonyPrefix]
  private static bool Prefix()
  {
    // The editor reads raw Unity keyboard/mouse input throughout Update,
    // including Escape before HandleKeyboardActions / HandleMouseActions.
    return !UIInteractionState.BlocksPointerInput;
  }
}

[HarmonyPatch(typeof(scrController), "Update")]
internal static class SettingsControllerShortcutPatch
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
  private static readonly MethodInfo ReadKeyDownMethod = AccessTools.Method(
    typeof(SettingsControllerShortcutPatch),
    nameof(ReadKeyDown)
  );
  private static readonly MethodInfo ReadKeyMethod = AccessTools.Method(
    typeof(SettingsControllerShortcutPatch),
    nameof(ReadKey)
  );

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
    return !UIInteractionState.BlocksPointerInput && UnityEngine.Input.GetKeyDown(key);
  }

  private static bool ReadKey(KeyCode key)
  {
    return !UIInteractionState.BlocksPointerInput && UnityEngine.Input.GetKey(key);
  }
}
