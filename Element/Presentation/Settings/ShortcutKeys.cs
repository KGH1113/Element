using Element.Configuration;
using UnityEngine;

namespace Element.Presentation.Settings;

/// <summary>Reads shortcuts from the keyboard and names their keys for display.</summary>
internal static class ShortcutKeys
{
  private static bool Korean => RDString.language == SystemLanguage.Korean;

  internal static bool IsMac =>
    UnityEngine.Application.platform == RuntimePlatform.OSXPlayer
    || UnityEngine.Application.platform == RuntimePlatform.OSXEditor;

  /// <summary>The key went down this frame with exactly this modifier held, and no other.</summary>
  public static bool Pressed(KeyCode key, KeyCode modifier)
  {
    if (key == KeyCode.None || !UnityEngine.Input.GetKeyDown(key))
      return false;
    foreach (KeyCode candidate in KeyShortcut.Modifiers)
      if (UnityEngine.Input.GetKey(candidate) != (candidate == modifier))
        return false;
    return true;
  }

  /// <summary>The held modifier, or None; <paramref name="count"/> tells when more than one is held.</summary>
  public static KeyCode HeldModifier(out int count)
  {
    KeyCode modifier = KeyCode.None;
    count = 0;
    foreach (KeyCode candidate in KeyShortcut.Modifiers)
      if (UnityEngine.Input.GetKey(candidate))
      {
        modifier = candidate;
        count++;
      }
    return modifier;
  }

  /// <summary>Such as "Left Alt + Tab"; macOS modifiers use their ⌥ ⌘ symbols.</summary>
  public static string Label(KeyCode key, KeyCode modifier) =>
    (modifier == KeyCode.None ? "" : ModifierLabel(modifier) + " + ") + KeyLabel(key);

  /// <summary>Like <see cref="Label"/> with modifiers spelled out, for the overlay font that has no ⌥ ⌘ glyphs.</summary>
  public static string TextLabel(KeyCode key, KeyCode modifier) =>
    (modifier == KeyCode.None ? "" : ModifierName(modifier) + " + ") + KeyLabel(key);

  internal static string ModifierLabel(KeyCode key)
  {
    if (IsMac && SettingsKeySymbol.Symbol(key).Length > 0)
      return (IsRightModifier(key) ? L("오른쪽 ", "Right ") : L("왼쪽 ", "Left ")) + SettingsKeySymbol.Symbol(key);
    return ModifierName(key);
  }

  /// <summary>The modifier spelled out, without the macOS symbol glyphs.</summary>
  internal static string ModifierName(KeyCode key)
  {
    string modifier = key.ToString()
      .Replace("Left", L("왼쪽 ", "Left "))
      .Replace("Right", L("오른쪽 ", "Right "))
      .Replace("Control", "Ctrl")
      .Replace("Command", "Cmd");
    return IsMac ? modifier.Replace("Alt", "Option") : modifier;
  }

  internal static bool IsRightModifier(KeyCode key) =>
    key == KeyCode.RightAlt || key == KeyCode.RightCommand || key == KeyCode.RightControl || key == KeyCode.RightShift;

  internal static string KeyLabel(KeyCode key)
  {
    if (key >= KeyCode.A && key <= KeyCode.Z)
      return key.ToString().ToUpperInvariant();
    if ((int)key >= 33 && (int)key <= 126)
      return ((char)key).ToString();
    if (key >= KeyCode.Keypad0 && key <= KeyCode.Keypad9)
      return "Num " + ((int)key - (int)KeyCode.Keypad0);
    return key switch
    {
      KeyCode.Escape => "Esc",
      KeyCode.Return => "Enter",
      KeyCode.Backspace => IsMac ? "Delete" : "Backspace",
      KeyCode.Delete => "Del",
      KeyCode.CapsLock => "Caps Lock",
      KeyCode.PageUp => "Page Up",
      KeyCode.PageDown => "Page Down",
      KeyCode.UpArrow => "↑",
      KeyCode.DownArrow => "↓",
      KeyCode.LeftArrow => "←",
      KeyCode.RightArrow => "→",
      KeyCode.KeypadPeriod => "Num .",
      KeyCode.KeypadDivide => "Num /",
      KeyCode.KeypadMultiply => "Num *",
      KeyCode.KeypadMinus => "Num -",
      KeyCode.KeypadPlus => "Num +",
      KeyCode.KeypadEnter => "Num Enter",
      KeyCode.KeypadEquals => "Num =",
      _ => key.ToString(),
    };
  }

  private static string L(string ko, string en) => Korean ? ko : en;
}
