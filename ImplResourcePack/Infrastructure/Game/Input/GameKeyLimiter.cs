using System;
using System.Collections.Generic;
using HarmonyLib;
using ImplResourcePack.Domain.Input;
using UnityEngine;

namespace ImplResourcePack.Infrastructure.Game.Input;

/// <summary>
/// Applies the ImplDmNote key profile through ADOFAI's own key limiter
/// (<c>Persistence.keyLimiterKeys</c>). The game filters both Unity and async
/// keyboard input with it on the main thread, so no input hook is needed.
/// Only the in-memory caches change: the player's saved keys stay on disk and
/// return as soon as ImplDmNote lets go, even after a crash.
/// </summary>
internal sealed class GameKeyLimiter : IDisposable
{
  private static readonly AccessTools.FieldRef<KeysSetting, HashSet<KeyCode>> UnityCache = AccessTools.FieldRefAccess<
    KeysSetting,
    HashSet<KeyCode>
  >("_unityKeysCache");
  private static readonly AccessTools.FieldRef<KeysSetting, HashSet<ushort>> AsyncCache = AccessTools.FieldRefAccess<
    KeysSetting,
    HashSet<ushort>
  >("_asyncKeysCache");

  private readonly Func<KeyLimiterSnapshot> _current;
  private KeyLimiterSnapshot _applied;
  private HashSet<KeyCode> _unityKeys;
  private HashSet<ushort> _asyncKeys;

  public GameKeyLimiter(Func<KeyLimiterSnapshot> current) => _current = current;

  /// <summary>True while the game's limiter uses ImplDmNote's keys. Main thread only.</summary>
  public static bool Active { get; private set; }

  /// <summary>Main thread, once per frame: two reference checks unless the profile changed.</summary>
  public void Tick()
  {
    KeyLimiterSnapshot next = _current();
    if (!ReferenceEquals(next, _applied))
    {
      Apply(next);
      return;
    }
    // The pause menu's own key editor replaces the caches through the game's
    // setters; put ImplDmNote's keys back while its profile is active.
    if (
      _unityKeys != null
      && (
        !ReferenceEquals(UnityCache(Persistence.keyLimiterKeys), _unityKeys)
        || !ReferenceEquals(AsyncCache(Persistence.keyLimiterKeys), _asyncKeys)
      )
    )
      Install();
  }

  public void Dispose()
  {
    Release();
    _applied = null;
  }

  private void Apply(KeyLimiterSnapshot snapshot)
  {
    _applied = snapshot;
    if (snapshot == null || !snapshot.Enabled)
    {
      Release();
      return;
    }
    // An empty set means "no limit" to the game. A sentinel nobody can press
    // keeps the other side limited when only Unity or only async keys resolved.
    HashSet<KeyCode> unityKeys = new(snapshot.AllowedKeys);
    if (unityKeys.Count == 0)
      unityKeys.Add(KeyCode.None);
    HashSet<ushort> asyncKeys = new(snapshot.AllowedAsyncKeys);
    if (asyncKeys.Count == 0)
      asyncKeys.Add(ushort.MaxValue);
    // The game may be reading the previous sets; publish new ones instead of mutating.
    _unityKeys = unityKeys;
    _asyncKeys = asyncKeys;
    Install();
  }

  private void Install()
  {
    KeysSetting keys = Persistence.keyLimiterKeys;
    UnityCache(keys) = _unityKeys;
    AsyncCache(keys) = _asyncKeys;
    SetActive(true);
  }

  // Clearing the caches makes the game reload the player's saved keys lazily.
  private void Release()
  {
    if (_unityKeys != null)
    {
      KeysSetting keys = Persistence.keyLimiterKeys;
      if (ReferenceEquals(UnityCache(keys), _unityKeys))
        UnityCache(keys) = null;
      if (ReferenceEquals(AsyncCache(keys), _asyncKeys))
        AsyncCache(keys) = null;
    }
    _unityKeys = null;
    _asyncKeys = null;
    SetActive(false);
  }

  private static void SetActive(bool active)
  {
    if (Active == active)
      return;
    Active = active;
    KeyLimiterMenuRow.Refresh();
  }
}
