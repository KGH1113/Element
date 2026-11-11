using Element.Application.Input;
using UnityEngine;

namespace Element.Presentation.Settings;

/// <summary>
/// Closes the input gate and shows the settings cursor while the settings window
/// or the layout editor is open; puts the game's cursor back when they close.
/// </summary>
internal static class SettingsInputCapture
{
  private static bool _previousCursorVisible;
  private static CursorLockMode _previousCursorLock;

  public static void Begin(bool layoutEditing = false)
  {
    if (!InputGate.IsActive)
    {
      _previousCursorVisible = Cursor.visible;
      _previousCursorLock = Cursor.lockState;
    }

    InputGate.Begin(layoutEditing);
    RestoreCursor();
  }

  public static void End()
  {
    if (!InputGate.IsActive)
      return;

    InputGate.End();
    SettingsCursor.ResetCursor();
    Cursor.lockState = _previousCursorLock;
    Cursor.visible = _previousCursorVisible;
  }

  public static void RestoreCursor()
  {
    if (!InputGate.IsActive)
      return;

    Cursor.lockState = CursorLockMode.None;
    Cursor.visible = true;
    SettingsCursor.Restore();
  }

  public static void Reset()
  {
    End();
    InputGate.Reset();
  }
}
