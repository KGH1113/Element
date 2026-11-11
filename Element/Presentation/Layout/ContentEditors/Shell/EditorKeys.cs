using UnityEngine;

namespace Element.Presentation.Layout.ContentEditors.Shell;

/// <summary>
/// Keys as the layout editor and its full-screen editors read them. The
/// layout canvas polls keys held this frame; the full-screen editors read
/// key presses as events, with Cmd on macOS and Ctrl elsewhere.
/// </summary>
internal static class EditorKeys
{
  /// <summary>Whether this runs on macOS, where Cmd is the command key.</summary>
  public static bool Mac =>
    UnityEngine.Application.platform == RuntimePlatform.OSXPlayer
    || UnityEngine.Application.platform == RuntimePlatform.OSXEditor;

  /// <summary>Ctrl or Cmd held this frame, on any system.</summary>
  public static bool CommandHeld =>
    Input.GetKey(KeyCode.LeftControl)
    || Input.GetKey(KeyCode.RightControl)
    || Input.GetKey(KeyCode.LeftCommand)
    || Input.GetKey(KeyCode.RightCommand);

  public static bool ShiftHeld => Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

  public static bool AltHeld => Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);

  /// <summary>Return or the keypad's Enter pressed this frame.</summary>
  public static bool EnterPressed => Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter);

  public static bool Pressed(KeyCode key) => Input.GetKeyDown(key);

  /// <summary>Takes the next key press from the event queue into <paramref name="e"/>; false when none is left.</summary>
  public static bool NextKeyDown(Event e)
  {
    while (Event.PopEvent(e))
      if (e.rawType == EventType.KeyDown)
        return true;
    return false;
  }

  /// <summary>Cmd on macOS, Ctrl elsewhere, held with the key in <paramref name="e"/>.</summary>
  public static bool Command(Event e, bool mac) => mac ? e.command : e.control;

  /// <summary>The word modifier: Option on macOS, Ctrl elsewhere.</summary>
  public static bool Word(Event e, bool mac) => mac ? e.alt : e.control;

  public static bool IsEnter(KeyCode key) => key == KeyCode.Return || key == KeyCode.KeypadEnter;
}
