using System;
using UnityEngine;

namespace Element.Presentation.Settings;

/// <summary>
/// The modifier keys held now. Where the legacy input isn't active (as in the
/// editor's UI preview) they read as not held.
/// </summary>
internal static class SettingsModifierKeys
{
  public static bool Alt => IsHeld(KeyCode.LeftAlt) || IsHeld(KeyCode.RightAlt);
  public static bool Shift => IsHeld(KeyCode.LeftShift) || IsHeld(KeyCode.RightShift);

  public static bool IsHeld(KeyCode key)
  {
    try
    {
      return Input.GetKey(key);
    }
    catch (InvalidOperationException)
    {
      return false;
    }
  }
}
