using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using SkyHook;
using UnityEngine;

namespace Element.Infrastructure.Game.Input;

internal readonly struct ResolvedLimiterKey
{
  public KeyCode UnityKey { get; }
  public bool HasAsyncKey { get; }
  public ushort AsyncKey { get; }

  public ResolvedLimiterKey(KeyCode unityKey, bool hasAsyncKey, ushort asyncKey)
  {
    UnityKey = unityKey;
    HasAsyncKey = hasAsyncKey;
    AsyncKey = asyncKey;
  }
}

internal static class DmNoteKeyMapper
{
  private static readonly Dictionary<string, KeyCode> NamedKeys = CreateNamedKeys();

  internal static void AddAsyncKeys(ResolvedLimiterKey resolved, HashSet<ushort> keys)
  {
    if (resolved.HasAsyncKey)
      keys.Add(resolved.AsyncKey);

    // SkyHook maps both ANSI (0x31) and ISO (0x64) HID usages to BackSlash,
    // but its reverse lookup returns only the ANSI usage.
    if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX) && resolved.UnityKey == KeyCode.Backslash)
    {
      keys.Add(0x31);
      keys.Add(0x64);
    }
  }

  public static bool TryResolve(string rawKey, out ResolvedLimiterKey resolved)
  {
    resolved = default;
    string key = Normalize(rawKey);
    if (string.IsNullOrEmpty(key) || !NamedKeys.TryGetValue(key, out KeyCode unityKey))
      return false;

    KeyLabel label = SkyHookKeyMapper.UnityKeyToSkyHookKey(unityKey);
    if (label == KeyLabel.Unknown)
    {
      resolved = new ResolvedLimiterKey(unityKey, false, 0);
      return true;
    }

    try
    {
      if (
        RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
        && TryGetMacAsyncKey(unityKey, out ushort macAsyncKey)
      )
      {
        resolved = new ResolvedLimiterKey(unityKey, true, macAsyncKey);
        return true;
      }

      ushort asyncKey = SkyHookKeyMapper.KeyLabelToNativeKeyCode(label);
      if (asyncKey == ushort.MaxValue)
      {
        resolved = new ResolvedLimiterKey(unityKey, false, 0);
        return true;
      }
      resolved = new ResolvedLimiterKey(unityKey, true, asyncKey);
    }
    catch
    {
      // Unity input can still be limited when a platform has no SkyHook mapping.
      resolved = new ResolvedLimiterKey(unityKey, false, 0);
    }
    return true;
  }

  // SkyHook's macOS reverse mapper does not resolve Return, returns
  // ushort.MaxValue for Shift, and does not preserve the left/right
  // distinction for Command. Its callback and RDInput AsyncKeyCode values use
  // USB HID usages directly.
  internal static bool TryGetMacAsyncKey(KeyCode unityKey, out ushort asyncKey)
  {
    if (unityKey == KeyCode.Return)
      asyncKey = 0x28;
    else if (unityKey == KeyCode.KeypadEnter)
      asyncKey = 0x58;
    else if (unityKey == KeyCode.LeftControl)
      asyncKey = 0xE0;
    else if (unityKey == KeyCode.LeftShift)
      asyncKey = 0xE1;
    else if (unityKey == KeyCode.LeftAlt)
      asyncKey = 0xE2;
    else if (unityKey == KeyCode.LeftMeta)
      asyncKey = 0xE3;
    else if (unityKey == KeyCode.RightControl)
      asyncKey = 0xE4;
    else if (unityKey == KeyCode.RightShift)
      asyncKey = 0xE5;
    else if (unityKey == KeyCode.RightAlt)
      asyncKey = 0xE6;
    else if (unityKey == KeyCode.RightMeta)
      asyncKey = 0xE7;
    else
    {
      asyncKey = 0;
      return false;
    }

    return true;
  }

  internal static string Normalize(string value) =>
    string.IsNullOrWhiteSpace(value)
      ? string.Empty
      : value.Trim().Replace(" ", string.Empty).Replace("_", string.Empty).ToUpperInvariant();

  private static Dictionary<string, KeyCode> CreateNamedKeys()
  {
    Dictionary<string, KeyCode> keys = new(StringComparer.OrdinalIgnoreCase);
    for (char value = 'A'; value <= 'Z'; value++)
      keys[value.ToString()] = (KeyCode)((int)KeyCode.A + value - 'A');
    for (int value = 0; value <= 9; value++)
      keys[value.ToString()] = (KeyCode)((int)KeyCode.Alpha0 + value);

    Add(keys, KeyCode.LeftShift, "LEFTSHIFT", "LSHIFT");
    Add(keys, KeyCode.RightShift, "RIGHTSHIFT", "RSHIFT");
    Add(keys, KeyCode.LeftControl, "LEFTCTRL", "LEFTCONTROL", "LCTRL");
    Add(keys, KeyCode.RightControl, "RIGHTCTRL", "RIGHTCONTROL", "RCTRL", "25");
    Add(keys, KeyCode.LeftAlt, "LEFTALT", "LALT");
    Add(keys, KeyCode.RightAlt, "RIGHTALT", "RALT", "21");
    Add(keys, KeyCode.LeftMeta, "LEFTMETA", "LEFTWIN", "91");
    Add(keys, KeyCode.RightMeta, "RIGHTMETA", "RIGHTWIN", "92");
    Add(keys, KeyCode.Backspace, "BACKSPACE");
    Add(keys, KeyCode.Tab, "TAB");
    Add(keys, KeyCode.Return, "RETURN", "ENTER");
    Add(keys, KeyCode.Pause, "PAUSE", "19");
    Add(keys, KeyCode.Escape, "ESCAPE", "ESC");
    Add(keys, KeyCode.Space, "SPACE");
    Add(keys, KeyCode.CapsLock, "CAPSLOCK");
    Add(keys, KeyCode.ScrollLock, "SCROLLLOCK");
    Add(keys, KeyCode.Numlock, "NUMLOCK");
    Add(keys, KeyCode.Print, "PRINTSCREEN");
    Add(keys, KeyCode.Insert, "INS", "INSERT");
    Add(keys, KeyCode.Delete, "DELETE", "DEL");
    Add(keys, KeyCode.Home, "HOME");
    Add(keys, KeyCode.End, "END");
    Add(keys, KeyCode.PageUp, "PAGEUP");
    Add(keys, KeyCode.PageDown, "PAGEDOWN");
    Add(keys, KeyCode.UpArrow, "UPARROW");
    Add(keys, KeyCode.DownArrow, "DOWNARROW");
    Add(keys, KeyCode.LeftArrow, "LEFTARROW");
    Add(keys, KeyCode.RightArrow, "RIGHTARROW");
    Add(keys, KeyCode.Minus, "MINUS", "-");
    Add(keys, KeyCode.Equals, "EQUAL", "EQUALS", "=");
    Add(keys, KeyCode.LeftBracket, "SQUAREBRACKETOPEN", "LEFTBRACKET", "[");
    Add(keys, KeyCode.RightBracket, "SQUAREBRACKETCLOSE", "RIGHTBRACKET", "]");
    Add(keys, KeyCode.Semicolon, "SEMICOLON", ";");
    Add(keys, KeyCode.Quote, "QUOTE", "'");
    Add(keys, KeyCode.BackQuote, "SECTION", "BACKQUOTE", "`");
    Add(keys, KeyCode.Backslash, "BACKSLASH", "\\");
    Add(keys, KeyCode.Comma, "COMMA", ",");
    Add(keys, KeyCode.Period, "DOT", "PERIOD", ".");
    Add(keys, KeyCode.Slash, "FORWARDSLASH", "SLASH", "/");

    for (int value = 0; value <= 9; value++)
      keys["NUMPAD" + value] = (KeyCode)((int)KeyCode.Keypad0 + value);
    Add(keys, KeyCode.KeypadMultiply, "NUMPADMULTIPLY");
    Add(keys, KeyCode.KeypadPlus, "NUMPADPLUS");
    Add(keys, KeyCode.KeypadMinus, "NUMPADMINUS");
    Add(keys, KeyCode.KeypadPeriod, "NUMPADDELETE", "NUMPADDOT", "NUMPADDECIMAL");
    Add(keys, KeyCode.KeypadDivide, "NUMPADDIVIDE");
    Add(keys, KeyCode.KeypadEnter, "NUMPADRETURN", "NUMPADENTER");

    for (int value = 1; value <= 15; value++)
      keys["F" + value] = (KeyCode)((int)KeyCode.F1 + value - 1);

    Add(keys, KeyCode.Mouse0, "MOUSE1");
    Add(keys, KeyCode.Mouse1, "MOUSE2");
    Add(keys, KeyCode.Mouse2, "MOUSE3");
    Add(keys, KeyCode.Mouse3, "MOUSE4");
    Add(keys, KeyCode.Mouse4, "MOUSE5");

    Add(keys, KeyCode.JoystickButton0, "GPA");
    Add(keys, KeyCode.JoystickButton1, "GPB");
    Add(keys, KeyCode.JoystickButton2, "GPX");
    Add(keys, KeyCode.JoystickButton3, "GPY");
    Add(keys, KeyCode.JoystickButton4, "GPLB");
    Add(keys, KeyCode.JoystickButton5, "GPRB");
    Add(keys, KeyCode.JoystickButton6, "GPLT");
    Add(keys, KeyCode.JoystickButton7, "GPRT");
    Add(keys, KeyCode.JoystickButton8, "GPBACK");
    Add(keys, KeyCode.JoystickButton9, "GPSTART");
    Add(keys, KeyCode.JoystickButton10, "GPLS");
    Add(keys, KeyCode.JoystickButton11, "GPRS");
    Add(keys, KeyCode.JoystickButton12, "GPUP");
    Add(keys, KeyCode.JoystickButton13, "GPDOWN");
    Add(keys, KeyCode.JoystickButton14, "GPLEFT");
    Add(keys, KeyCode.JoystickButton15, "GPRIGHT");
    return keys;
  }

  private static void Add(Dictionary<string, KeyCode> target, KeyCode key, params string[] names)
  {
    foreach (string name in names)
      target[Normalize(name)] = key;
  }
}
