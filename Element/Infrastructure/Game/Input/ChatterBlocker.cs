using System;
using System.Collections.Generic;
using Element.Domain.Input;
using Element.Infrastructure.ElementNote.Keys;
using SkyHook;
using UnityEngine;

namespace Element.Infrastructure.Game.Input;

/// <summary>
/// Key chatter blocking on ADOFAI's two keyboard paths, where
/// KeyboardChatterBlocker blocks it. Async presses are dropped where SkyHook
/// delivers them, so the game, key viewers and other mods all see the same
/// clean stream. Unity input presses are taken out of the count of keys that
/// hit a tile. One lives as long as the game, so its count covers the whole run.
/// </summary>
internal sealed class ChatterBlocker
{
  private readonly ChatterFilter _async = new(sameFrame: false);
  private readonly ChatterFilter _sync = new(sameFrame: true);

  // This frame's presses; the game asks several times a frame.
  private readonly List<AnyKeyCode> _presses = new(16);
  private int _frame = -1;
  private int _blockedThisFrame;
  private string _ignoredShape;
  private bool[] _ignoredSync,
    _ignoredAsync;

  public long Blocked => _async.Blocked + _sync.Blocked;

  // KeyboardChatterBlocker's clock: wall time in whole milliseconds, read as
  // the press is handled. Safe on any thread and allocation free.
  private static long NowMs => DateTime.UtcNow.Ticks / TimeSpan.TicksPerMillisecond;

  /// <summary>Main thread; the async filter picks the change up on its next press.</summary>
  public void Configure(bool enabled, int windowMs, IReadOnlyList<KeyCode> ignored)
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
          ElementNoteKeyMapper.AddAsyncKeys(ElementNoteKeyMapper.Resolve(key), asyncKeys);
        }
      foreach (ushort key in asyncKeys)
        if (key < ChatterFilter.KeyCount)
          _ignoredAsync[key] = true;
    }
    _async.Configure(enabled, windowMs, _ignoredAsync);
    _sync.Configure(enabled, windowMs, _ignoredSync);
  }

  /// <summary>
  /// SkyHook's native thread: no Unity calls, no allocation, and nothing may
  /// escape into native code. Releases always pass.
  /// </summary>
  public bool AcceptAsync(SkyHookEvent ev)
  {
    try
    {
      return ev.Type != SkyHook.EventType.KeyPressed
        || ev.Label == KeyLabel.Escape
        || _async.AcceptPress(ev.Key, NowMs);
    }
    catch
    {
      return true;
    }
  }

  /// <summary>
  /// Main thread. The game counts every press, touches and gamepads included; only the
  /// chatter of Unity key presses comes off. Co-op counts presses per player, which this
  /// count can't split, so it is left as is. A count of zero, as while the settings
  /// window holds input, leaves press times alone.
  /// </summary>
  public void RemoveChatter(ref int validKeys)
  {
    if (validKeys <= 0 || !_sync.Enabled || scrController.coopMode)
      return;
    int blocked = BlockedSyncPresses();
    if (blocked > 0)
      validKeys = Math.Max(0, validKeys - blocked);
  }

  /// <summary>Main thread. Unity key presses dropped as chatter this frame, judged once per frame.</summary>
  private int BlockedSyncPresses()
  {
    int frame = Time.frameCount;
    if (frame == _frame)
      return _blockedThisFrame;
    _frame = frame;
    _blockedThisFrame = 0;
    RDInput.GetMainPressKeys(_presses);
    long now = NowMs;
    foreach (AnyKeyCode press in _presses)
      if (press.value is KeyCode key && !_sync.AcceptPress((int)key, now))
        _blockedThisFrame++;
    _presses.Clear();
    return _blockedThisFrame;
  }
}
