using System;
using System.Collections.Generic;
using HarmonyLib;
using ImplResourcePack.Bootstrap;
using ImplResourcePack.Domain.Input;
using SkyHook;
using UnityEngine;

namespace ImplResourcePack.Infrastructure.Game.Input;

/// <summary>
/// Key chatter blocking for both of ADOFAI's keyboard paths. Async input is
/// filtered where SkyHook delivers it, so the game, key viewers and other mods
/// all see the same clean stream. Unity input is filtered in the game's own
/// per-frame press cache.
/// </summary>
internal static class ChatterBlocker
{
  internal static readonly ChatterFilter Async = new();
  internal static readonly ChatterFilter Sync = new();

  // Keys pressed through Unity input, polled for release. A handful at most.
  private static readonly List<KeyCode> HeldSyncKeys = new(32);

  public static long Blocked => Async.Blocked + Sync.Blocked;

  /// <summary>Main thread; the async filter picks the change up on its next event.</summary>
  public static void Configure(bool enabled, int windowMs)
  {
    Async.Configure(enabled, windowMs);
    Sync.Configure(enabled, windowMs);
    if (!enabled)
      HeldSyncKeys.Clear();
  }

  /// <summary>Main thread. Removes chatter from this frame's freshly computed presses.</summary>
  internal static int FilterSyncPresses(List<AnyKeyCode> presses)
  {
    long now = (long)(Time.unscaledTimeAsDouble * TimeSpan.TicksPerSecond);
    // Unity input has no release events here; a held key that is up now was
    // released during the last frame.
    for (int index = HeldSyncKeys.Count - 1; index >= 0; index--)
    {
      KeyCode held = HeldSyncKeys[index];
      if (UnityEngine.Input.GetKey(held))
        continue;
      Sync.Accept((int)held, false, now);
      HeldSyncKeys.RemoveAt(index);
    }
    for (int index = presses.Count - 1; index >= 0; index--)
    {
      if (presses[index].value is not KeyCode key)
        continue;
      if (!HeldSyncKeys.Contains(key))
        HeldSyncKeys.Add(key);
      if (!Sync.Accept((int)key, true, now))
        presses.RemoveAt(index);
    }
    return presses.Count;
  }
}

[HarmonyPatch(typeof(SkyHookManager), "HookCallback")]
[PatchGroup("ChatterBlocker")]
internal static class AsyncChatterPatch
{
  // SkyHook's native thread: no Unity calls, no allocation, and nothing may
  // escape into native code.
  [HarmonyPrefix]
  [HarmonyPriority(Priority.First)]
  private static bool Prefix(SkyHookEvent ev)
  {
    try
    {
      return ev.Label == KeyLabel.Escape
        || ChatterBlocker.Async.Accept(ev.Key, ev.Type == SkyHook.EventType.KeyPressed, ev.GetTimeInTicks());
    }
    catch
    {
      return true;
    }
  }
}

[HarmonyPatch(typeof(RDInputType_Keyboard), nameof(RDInputType_Keyboard.MainIgnoreActive))]
[PatchGroup("ChatterBlocker")]
internal static class SyncChatterPatch
{
  // Only a fresh press computation is filtered; later calls in the same frame
  // return the cached, already filtered list.
  [HarmonyPrefix]
  private static void Prefix(RDInputType_Keyboard __instance, ButtonState state, out bool __state) =>
    __state =
      state == ButtonState.WentDown
      && ChatterBlocker.Sync.Enabled
      && __instance.isActive
      && __instance.pressCount.lastFrameUpdated != Time.frameCount;

  [HarmonyPostfix]
  private static void Postfix(RDInputType_Keyboard __instance, bool __state, ref int __result)
  {
    if (__state)
      __result = ChatterBlocker.FilterSyncPresses(__instance.pressCount.keys);
  }
}
