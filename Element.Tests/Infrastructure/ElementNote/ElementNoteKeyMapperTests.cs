using Element.Infrastructure.ElementNote.Keys;
using Element.Tests.Support;
using UnityEngine;
using static Element.Tests.Support.Assert;

namespace Element.Tests.Infrastructure.ElementNote;

/// <summary>ElementNote's key names, read as Unity keys and macOS key codes.</summary>
internal static class ElementNoteKeyMapperTests
{
  [Suite("normalizes DM Note names")]
  public static void NormalizeNames()
  {
    Equal("LEFTSHIFT", ElementNoteKeyMapper.Normalize(" left shift "));
    Equal("GPA", ElementNoteKeyMapper.Normalize("GP_A"));
  }

  [Suite("maps representative keyboard, mouse, and gamepad keys")]
  public static void MapsRepresentativeKeys()
  {
    MapsTo("A", KeyCode.A);
    MapsTo("LEFT SHIFT", KeyCode.LeftShift);
    MapsTo("25", KeyCode.RightControl);
    MapsTo("FORWARD SLASH", KeyCode.Slash);
    MapsTo("NUMPAD RETURN", KeyCode.KeypadEnter);
    MapsTo("MOUSE5", KeyCode.Mouse4);
    MapsTo("GP_A", KeyCode.JoystickButton0);
    MapsTo("GP_RIGHT", KeyCode.JoystickButton15);
  }

  [Suite("maps macOS modifier async keys")]
  public static void MapsMacModifierAsyncKeys()
  {
    MacModifierMapsTo(KeyCode.LeftControl, 0xE0);
    MacModifierMapsTo(KeyCode.LeftShift, 0xE1);
    MacModifierMapsTo(KeyCode.LeftAlt, 0xE2);
    MacModifierMapsTo(KeyCode.LeftMeta, 0xE3);
    MacModifierMapsTo(KeyCode.RightControl, 0xE4);
    MacModifierMapsTo(KeyCode.RightShift, 0xE5);
    MacModifierMapsTo(KeyCode.RightAlt, 0xE6);
    MacModifierMapsTo(KeyCode.RightMeta, 0xE7);
    Check(!ElementNoteKeyMapper.TryGetMacAsyncKey(KeyCode.A, out _), "ordinary keys must not use the modifier fallback");
    MapsAsyncTo("LEFT SHIFT", KeyCode.LeftShift, 0xE1);
    MapsAsyncTo("RIGHT SHIFT", KeyCode.RightShift, 0xE5);
  }

  [Suite("maps Enter async keys")]
  public static void MapsEnterAsyncKeys()
  {
    MapsAsyncTo("RETURN", KeyCode.Return, 0x28);
    MapsAsyncTo("NUMPAD RETURN", KeyCode.KeypadEnter, 0x58);
  }

  private static void MapsTo(string input, KeyCode expected)
  {
    Check(ElementNoteKeyMapper.TryResolve(input, out ResolvedLimiterKey actual), input + " should map");
    Equal(expected, actual.UnityKey);
  }

  private static void MacModifierMapsTo(KeyCode key, ushort expected)
  {
    Check(ElementNoteKeyMapper.TryGetMacAsyncKey(key, out ushort actual), key + " should have a macOS async fallback");
    Equal(expected, actual);
  }

  private static void MapsAsyncTo(string input, KeyCode expectedKey, ushort expectedAsyncKey)
  {
    Check(ElementNoteKeyMapper.TryResolve(input, out ResolvedLimiterKey actual), input + " should map");
    Equal(expectedKey, actual.UnityKey);
    Check(actual.HasAsyncKey, input + " should have an async key");
    Equal(expectedAsyncKey, actual.AsyncKey);
  }
}
