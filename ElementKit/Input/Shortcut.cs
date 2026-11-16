using System;
using UnityEngine;

namespace ElementKit;

/// <summary>
/// A key with at most one modifier, the way Element's own shortcuts are. Keep
/// it in your settings class; players set it with
/// <see cref="EditorPage.Shortcut"/>, and <see cref="Pressed"/> tells when it is used.
/// </summary>
[Serializable]
public struct Shortcut
{
  private static readonly KeyCode[] Modifiers =
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

  /// <summary>The key; <see cref="KeyCode.None"/> for no shortcut.</summary>
  public KeyCode Key;

  /// <summary>The modifier held with it, such as <see cref="KeyCode.LeftControl"/>; None for none.</summary>
  public KeyCode Modifier;

  /// <summary>A shortcut of <paramref name="key"/>, held with <paramref name="modifier"/> when there is one.</summary>
  public Shortcut(KeyCode key, KeyCode modifier = KeyCode.None)
  {
    Key = key;
    Modifier = modifier;
  }

  /// <summary>Whether a shortcut is set.</summary>
  public bool IsSet => Key != KeyCode.None;

  /// <summary>
  /// The key went down this frame with exactly its modifier held, and no other.
  /// Call it from <c>Update</c> or <c>OnUpdate</c>, once a frame.
  /// </summary>
  public bool Pressed
  {
    get
    {
      if (Key == KeyCode.None || !Input.GetKeyDown(Key))
        return false;
      foreach (KeyCode candidate in Modifiers)
        if (Input.GetKey(candidate) != (candidate == Modifier))
          return false;
      return true;
    }
  }

  internal static bool IsModifier(KeyCode key) => Array.IndexOf(Modifiers, key) >= 0;
}
