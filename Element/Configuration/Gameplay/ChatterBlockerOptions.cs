using System;
using System.Collections.Generic;
using UnityEngine;

namespace Element.Configuration.Gameplay;

[CopiedInPlace]
public sealed class ChatterBlockerOptions
{
  public const int MinimumWindowMs = 10;
  public const int MaximumWindowMs = 500;
  public const int DefaultWindowMs = 100;

  /// <summary>The keys the chatter filter tells apart; one past them can't be ignored.</summary>
  public const int KeyCount = 1024;

  public bool Enabled = true;

  /// <summary>A press of a key no later than this after its last accepted press is chatter.</summary>
  public int WindowMs = DefaultWindowMs;

  /// <summary>Keys never filtered, such as one used for fast jacks.</summary>
  public List<KeyCode> IgnoredKeys = new();

  public bool Ignores(KeyCode key) => IgnoredKeys != null && IgnoredKeys.Contains(key);

  /// <summary>Adds the key to the ignored keys, or takes it off when it is already there.</summary>
  public void ToggleIgnored(KeyCode key)
  {
    IgnoredKeys ??= new();
    if (!IgnoredKeys.Remove(key) && IsIgnorable(key))
      IgnoredKeys.Add(key);
  }

  /// <summary>Keyboard keys only, as KeyboardChatterBlocker lets you choose.</summary>
  public static bool IsIgnorable(KeyCode key) => key != KeyCode.None && key < KeyCode.Mouse0 && (int)key < KeyCount;

  public static int ClampWindow(int milliseconds) => Math.Max(MinimumWindowMs, Math.Min(MaximumWindowMs, milliseconds));

  public void Normalize()
  {
    WindowMs = ClampWindow(WindowMs);
    IgnoredKeys ??= new();
    var seen = new HashSet<KeyCode>();
    IgnoredKeys.RemoveAll(key => !IsIgnorable(key) || !seen.Add(key));
  }
}
