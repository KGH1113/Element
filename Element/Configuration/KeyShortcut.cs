using System;
using UnityEngine;

namespace Element.Configuration;

/// <summary>The rules every saved shortcut follows: one key, held with at most one modifier.</summary>
public static class KeyShortcut
{
  public static readonly KeyCode[] Modifiers =
  {
    KeyCode.LeftAlt,
    KeyCode.RightAlt,
    KeyCode.LeftControl,
    KeyCode.RightControl,
    KeyCode.LeftShift,
    KeyCode.RightShift,
    KeyCode.LeftCommand,
    KeyCode.RightCommand,
  };

  public static bool IsModifier(KeyCode key) => Array.IndexOf(Modifiers, key) >= 0;

  /// <summary>A keyboard key that is not a modifier, with no modifier or one of <see cref="Modifiers"/>.</summary>
  public static bool IsValid(KeyCode key, KeyCode modifier) =>
    key != KeyCode.None
    && key < KeyCode.Mouse0
    && !IsModifier(key)
    && (modifier == KeyCode.None || IsModifier(modifier));
}
