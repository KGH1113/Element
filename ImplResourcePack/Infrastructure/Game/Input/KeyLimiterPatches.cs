using System;
using HarmonyLib;
using ImplResourcePack.Bootstrap;
using ImplResourcePack.Domain.Input;
using SkyHook;
using UnityEngine;
using UnityEngine.Pool;

namespace ImplResourcePack.Infrastructure.Game.Input;

internal static class KeyLimiterPatchPolicy
{
  // SkyHook calls the hook prefix on its native thread, where Unity objects must
  // not be touched. The main thread publishes the gameplay state after each
  // controller update, and the hook reads only this flag and the snapshot.
  private static volatile bool _playerControl;

  public static bool ShouldEnforce(KeyLimiterSnapshot snapshot) =>
    snapshot != null && snapshot.Enabled && IsPlayerControl(ADOBase.controller);

  public static bool ShouldEnforceOnHookThread(KeyLimiterSnapshot snapshot) =>
    snapshot != null && snapshot.Enabled && _playerControl;

  public static void Publish(scrController controller) => Publish(IsPlayerControl(controller));

  internal static void Publish(bool playerControl) => _playerControl = playerControl;

  public static void Reset() => _playerControl = false;

  private static bool IsPlayerControl(scrController controller) =>
    controller != null
    && !controller.paused
    && controller.gameworld
    && controller.stateMachine.GetState() is States state
    && state == States.PlayerControl;
}

[HarmonyPatch(typeof(scrController), "Update")]
[PatchGroup("KeyLimiter")]
internal static class KeyLimiterStatePatch
{
  [HarmonyPostfix]
  private static void Postfix(scrController __instance) => KeyLimiterPatchPolicy.Publish(__instance);
}

[HarmonyPatch(typeof(scrPlayer), nameof(scrPlayer.CountValidKeysPressed))]
[HarmonyAfter("KeyboardChatterBlocker")]
[PatchGroup("KeyLimiter")]
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
[PatchGroup("KeyLimiter")]
internal static class SkyHookLimiterPatch
{
  [HarmonyPrefix]
  [HarmonyPriority(Priority.Last)]
  private static bool Prefix(SkyHookEvent ev)
  {
    if (ev.Type != SkyHook.EventType.KeyPressed || ev.Label == KeyLabel.Escape)
      return true;

    KeyLimiterSnapshot snapshot = Main.Instance?.Runtime?.KeyLimiter?.Current;
    return !KeyLimiterPatchPolicy.ShouldEnforceOnHookThread(snapshot) || snapshot.AllowsAsync(ev.Key);
  }
}
