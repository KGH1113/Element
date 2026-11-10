using System;
using System.Collections.Generic;
using Element.Bootstrap;
using Element.Domain.Input;
using HarmonyLib;
using SkyHook;
using UnityEngine;

namespace Element.Infrastructure.Game.Input;

/// <summary>
/// Key chatter blocking on ADOFAI's two keyboard paths, where
/// KeyboardChatterBlocker blocks it. Async presses are dropped where SkyHook
/// delivers them, so the game, key viewers and other mods all see the same
/// clean stream. Unity input presses are taken out of the count of keys that
/// hit a tile.
/// </summary>
internal static class ChatterBlocker
{
  internal static readonly ChatterFilter Async = new(sameFrame: false);
  internal static readonly ChatterFilter Sync = new(sameFrame: true);

  // This frame's presses; the game asks several times a frame.
  private static readonly List<AnyKeyCode> Presses = new(16);
  private static int _frame = -1;
  private static int _blockedThisFrame;
  private static string _ignoredShape;
  private static bool[] _ignoredSync,
    _ignoredAsync;

  public static long Blocked => Async.Blocked + Sync.Blocked;

  // KeyboardChatterBlocker's clock: wall time in whole milliseconds, read as
  // the press is handled. Safe on any thread and allocation free.
  internal static long NowMs => DateTime.UtcNow.Ticks / TimeSpan.TicksPerMillisecond;

  /// <summary>Main thread; the async filter picks the change up on its next press.</summary>
  public static void Configure(bool enabled, int windowMs, IReadOnlyList<KeyCode> ignored)
  {
    // Settings apply often; the key masks are rebuilt only when the list changed.
    string shape = ignored == null || ignored.Count == 0 ? "" : string.Join(",", ignored);
    if (_ignoredSync == null || shape != _ignoredShape)
    {
      _ignoredShape = shape;
      _ignoredSync = new bool[ChatterFilter.KeyCount];
      _ignoredAsync = new bool[ChatterFilter.KeyCount];
      var asyncKeys = new HashSet<ushort>();
      if (ignored != null)
        foreach (KeyCode key in ignored)
        {
          if ((uint)key < ChatterFilter.KeyCount)
            _ignoredSync[(int)key] = true;
          DmNoteKeyMapper.AddAsyncKeys(DmNoteKeyMapper.Resolve(key), asyncKeys);
        }
      foreach (ushort key in asyncKeys)
        if (key < ChatterFilter.KeyCount)
          _ignoredAsync[key] = true;
    }
    Async.Configure(enabled, windowMs, _ignoredAsync);
    Sync.Configure(enabled, windowMs, _ignoredSync);
  }

  /// <summary>Main thread. Unity key presses dropped as chatter this frame, judged once per frame.</summary>
  internal static int BlockedSyncPresses()
  {
    int frame = Time.frameCount;
    if (frame == _frame)
      return _blockedThisFrame;
    _frame = frame;
    _blockedThisFrame = 0;
    RDInput.GetMainPressKeys(Presses);
    long now = NowMs;
    foreach (AnyKeyCode press in Presses)
      if (press.value is KeyCode key && !Sync.AcceptPress((int)key, now))
        _blockedThisFrame++;
    Presses.Clear();
    return _blockedThisFrame;
  }
}

[HarmonyPatch(typeof(SkyHookManager), "HookCallback")]
[PatchGroup("ChatterBlocker")]
internal static class AsyncChatterPatch
{
  // SkyHook's native thread: no Unity calls, no allocation, and nothing may
  // escape into native code. Releases always pass.
  [HarmonyPrefix]
  [HarmonyPriority(Priority.First)]
  private static bool Prefix(SkyHookEvent ev)
  {
    try
    {
      return ev.Type != SkyHook.EventType.KeyPressed
        || ev.Label == KeyLabel.Escape
        || ChatterBlocker.Async.AcceptPress(ev.Key, ChatterBlocker.NowMs);
    }
    catch
    {
      return true;
    }
  }
}

[HarmonyPatch(typeof(scrPlayer), nameof(scrPlayer.CountValidKeysPressed))]
[PatchGroup("ChatterBlocker")]
internal static class SyncChatterPatch
{
  // The game counts every press, touches and gamepads included; only the
  // chatter of Unity key presses comes off. Co-op counts presses per player,
  // which this count can't split, so it is left as is. A count of zero, as
  // while the settings window holds input, leaves press times alone.
  [HarmonyPostfix]
  private static void Postfix(ref int __result)
  {
    if (__result <= 0 || !ChatterBlocker.Sync.Enabled || scrController.coopMode)
      return;
    int blocked = ChatterBlocker.BlockedSyncPresses();
    if (blocked > 0)
      __result = Math.Max(0, __result - blocked);
  }
}
