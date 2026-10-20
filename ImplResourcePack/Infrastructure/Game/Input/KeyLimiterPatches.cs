using System;
using HarmonyLib;
using ImplResourcePack.Domain.Input;
using SkyHook;
using UnityEngine;
using UnityEngine.Pool;

namespace ImplResourcePack.Infrastructure.Game.Input;

internal static class KeyLimiterPatchPolicy
{
  public static bool ShouldEnforce(KeyLimiterSnapshot snapshot)
  {
    scrController controller = ADOBase.controller;
    return snapshot != null
      && snapshot.Enabled
      && controller != null
      && !controller.paused
      && controller.gameworld
      && controller.stateMachine.GetState() is States state
      && state == States.PlayerControl;
  }
}

[HarmonyPatch(typeof(scrPlayer), nameof(scrPlayer.CountValidKeysPressed))]
[HarmonyAfter("KeyboardChatterBlocker")]
internal static class CountValidKeysPressedLimiterPatch
{
  [HarmonyPostfix]
  [HarmonyPriority(Priority.Last)]
  private static void Postfix(ref int __result)
  {
    KeyLimiterSnapshot snapshot = Main.Instance?.Runtime?.KeyLimiter?.Current;
    if (!KeyLimiterPatchPolicy.ShouldEnforce(snapshot))
      return;

    int blocked = 0;
    using (ListPool<AnyKeyCode>.Get(out var keys))
    {
      RDInput.GetMainPressKeys(keys);
      foreach (AnyKeyCode key in keys)
      {
        if (key.value is KeyCode unityKey && !snapshot.Allows(unityKey))
          blocked++;
        else if (key.value is AsyncKeyCode asyncKey && !snapshot.AllowsAsync(asyncKey.key))
          blocked++;
      }
    }
    __result = Math.Max(0, __result - blocked);
  }
}

[HarmonyPatch(typeof(SkyHookManager), "HookCallback")]
[HarmonyAfter("KeyboardChatterBlocker")]
internal static class SkyHookLimiterPatch
{
  [HarmonyPrefix]
  [HarmonyPriority(Priority.Last)]
  private static bool Prefix(SkyHookEvent ev)
  {
    if (ev.Type != SkyHook.EventType.KeyPressed || ev.Label == KeyLabel.Escape)
      return true;

    KeyLimiterSnapshot snapshot = Main.Instance?.Runtime?.KeyLimiter?.Current;
    return !KeyLimiterPatchPolicy.ShouldEnforce(snapshot) || snapshot.AllowsAsync(ev.Key);
  }
}
