using UnityEngine;

namespace ImplResourcePack.Presentation.Settings;

/// <summary>Owns input and cursor capture for the settings and layout surfaces.</summary>
internal static class UIInteractionState
{
  private static volatile bool _active;
  private static volatile bool _layoutEditing;
  private static volatile bool _textEditing;
  private static int _suppressThroughFrame = -1;
  private static bool _previousCursorVisible;
  private static CursorLockMode _previousCursorLock;

  // The native keyboard hook reads only these volatile values, never Unity APIs.
  public static bool IsActive => _active;
  public static bool IsLayoutEditing => _active && _layoutEditing;
  public static bool BlocksAsyncKeyboard => _active && (!_layoutEditing || _textEditing);

  public static void SetTextEditing(bool editing)
  {
    if (_textEditing == editing)
      return;
    _textEditing = editing;
    if (editing)
      ClearAsyncInput();
  }

  public static bool BlocksGameInput => BlocksAsyncKeyboard || Time.frameCount <= _suppressThroughFrame;
  public static bool BlocksPointerInput => _active || Time.frameCount <= _suppressThroughFrame;

  public static void Begin(bool layoutEditing = false)
  {
    if (!_active)
    {
      _previousCursorVisible = Cursor.visible;
      _previousCursorLock = Cursor.lockState;
    }

    bool captureKeyboard = !layoutEditing && (!_active || _layoutEditing);
    _layoutEditing = layoutEditing;
    _active = true;
    if (captureKeyboard)
      ClearAsyncInput();
    RestoreCursor();
  }

  public static void End()
  {
    if (!_active)
      return;

    bool hadKeyboardCapture = !_layoutEditing;
    _active = false;
    _layoutEditing = false;
    _textEditing = false;
    SettingsCursor.ResetCursor();
    // A close click or Escape must not also activate the game beneath the UI.
    _suppressThroughFrame = Time.frameCount;
    if (hadKeyboardCapture)
      ClearAsyncInput();
    Cursor.lockState = _previousCursorLock;
    Cursor.visible = _previousCursorVisible;
  }

  public static void RestoreCursor()
  {
    if (!_active)
      return;

    Cursor.lockState = CursorLockMode.None;
    Cursor.visible = true;
    SettingsCursor.Restore();
  }

  public static void Reset()
  {
    End();
    _suppressThroughFrame = -1;
  }

  internal static void ClearAsyncInput()
  {
    AsyncInputManager.ClearKeys();
    AsyncInputManager.frameDependentKeyMask.Clear();
    AsyncInputManager.frameDependentKeyDownMask.Clear();
    AsyncInputManager.frameDependentKeyUpMask.Clear();
  }
}
