using System;
using UnityEngine;

namespace Element.Application.Input;

/// <summary>
/// Whether the game may read the player's input while the settings window or the
/// layout editor is open. Game patches and the native keyboard hook read it; the
/// settings surfaces open and close it.
/// </summary>
internal static class InputGate
{
  private static volatile bool _active;
  private static volatile bool _layoutEditing;
  private static volatile bool _textEditing;
  private static int _suppressThroughFrame = -1;
  private static Action _clearGameKeys;

  // The native keyboard hook reads only these volatile values, never Unity APIs.
  public static bool IsActive => _active;
  public static bool IsLayoutEditing => _active && _layoutEditing;
  public static bool BlocksAsyncKeyboard => _active && (!_layoutEditing || _textEditing);
  public static bool BlocksGameInput => BlocksAsyncKeyboard || Time.frameCount <= _suppressThroughFrame;
  public static bool BlocksPointerInput => _active || Time.frameCount <= _suppressThroughFrame;

  /// <summary>How to drop the keys the game has queued; Bootstrap sets it once.</summary>
  public static void Connect(Action clearGameKeys) => _clearGameKeys = clearGameKeys;

  public static void SetTextEditing(bool editing)
  {
    if (_textEditing == editing)
      return;
    _textEditing = editing;
    if (editing)
      ClearGameKeys();
  }

  public static void Begin(bool layoutEditing = false)
  {
    bool captureKeyboard = !layoutEditing && (!_active || _layoutEditing);
    _layoutEditing = layoutEditing;
    _active = true;
    if (captureKeyboard)
      ClearGameKeys();
  }

  public static void End()
  {
    if (!_active)
      return;

    bool hadKeyboardCapture = !_layoutEditing;
    _active = false;
    _layoutEditing = false;
    _textEditing = false;
    // A close click or Escape must not also activate the game beneath the UI.
    _suppressThroughFrame = Time.frameCount;
    if (hadKeyboardCapture)
      ClearGameKeys();
  }

  public static void Reset()
  {
    End();
    _suppressThroughFrame = -1;
  }

  private static void ClearGameKeys() => _clearGameKeys?.Invoke();
}
