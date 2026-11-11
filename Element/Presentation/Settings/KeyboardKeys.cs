using System;
using System.Collections.Generic;
using UnityEngine;

namespace Element.Presentation.Settings;

/// <summary>Finds the keyboard keys pressed this frame, for fields that record keys.</summary>
internal static class KeyboardKeys
{
  // Every keyboard key once, in KeyCode order; mouse and joystick buttons left out.
  private static readonly KeyCode[] All = Collect();

  /// <summary>
  /// Steps <paramref name="index"/> to the next key that went down this frame,
  /// in KeyCode order; false when none is left. Start from -1.
  /// </summary>
  public static bool NextPressed(ref int index, out KeyCode key)
  {
    while (++index < All.Length)
      if (Input.GetKeyDown(All[index]))
      {
        key = All[index];
        return true;
      }
    key = KeyCode.None;
    return false;
  }

  private static KeyCode[] Collect()
  {
    var keys = new List<KeyCode>();
    foreach (KeyCode key in (KeyCode[])Enum.GetValues(typeof(KeyCode)))
      if (key != KeyCode.None && key < KeyCode.Mouse0 && !keys.Contains(key))
        keys.Add(key);
    return keys.ToArray();
  }
}
