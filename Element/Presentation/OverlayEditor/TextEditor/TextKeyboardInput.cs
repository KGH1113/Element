using Element.Presentation.OverlayEditor.Parts;
using Element.Presentation.OverlayEditor.Parts.Dock.Keypad;
using UnityEngine;

namespace Element.Presentation.OverlayEditor.TextEditor;

/// <summary>
/// Keys in the text editor: typing (with an input method's composition),
/// moving the caret by character, word and line, selecting, deleting,
/// the clipboard, undo, and ⌘/Ctrl+Enter or Esc to finish. While a number
/// or value is open, Enter and Esc close it and the keyboard types like the keypad.
/// </summary>
internal sealed class TextKeyboardInput
{
  private readonly ITextEditorHost _host;
  private readonly TextDocumentEditing _text;
  private readonly TextFormulas _formulas;
  private readonly TextLayoutMap _map;
  private readonly TextPreview _preview;
  private readonly Event _event = new();

  public TextKeyboardInput(
    ITextEditorHost host,
    TextDocumentEditing text,
    TextFormulas formulas,
    TextLayoutMap map,
    TextPreview preview
  )
  {
    _host = host;
    _text = text;
    _formulas = formulas;
    _map = map;
    _preview = preview;
  }

  public void HandleKeys()
  {
    bool mac = EditorKeys.Mac;
    while (_host.Screen != null && EditorKeys.NextKeyDown(_event))
    {
      bool command = EditorKeys.Command(_event, mac);
      bool word = EditorKeys.Word(_event, mac);
      bool shift = _event.shift;
      if (HandlePopoverKey(command))
        continue;
      int caret = _text.Caret;
      switch (_event.keyCode)
      {
        case KeyCode.Escape:
          _host.Escape();
          return;
        case KeyCode.Return:
        case KeyCode.KeypadEnter:
          if (command)
          {
            _host.Close(true, true);
            return;
          }
          if (_text.Composition.Length == 0)
            _text.Type("\n");
          continue;
        case KeyCode.Backspace:
          if (_text.Composition.Length > 0)
            continue;
          if (_text.HasSelection)
            _text.DeleteSelection();
          else if (caret > 0)
            _text.DeleteRange(
              word ? _text.Document.WordStart(caret)
                : command ? _map.LineStart(caret)
                : caret - 1,
              caret
            );
          continue;
        case KeyCode.Delete:
          if (_text.HasSelection)
            _text.DeleteSelection();
          else if (caret < _text.Document.Count)
            _text.DeleteRange(caret, word ? _text.Document.WordEnd(caret) : caret + 1);
          continue;
        case KeyCode.LeftArrow:
          MoveHorizontally(-1, shift, word, command);
          continue;
        case KeyCode.RightArrow:
          MoveHorizontally(1, shift, word, command);
          continue;
        case KeyCode.UpArrow:
          _text.MoveTo(command ? 0 : LineStep(-1), shift, true);
          continue;
        case KeyCode.DownArrow:
          _text.MoveTo(command ? _text.Document.Count : LineStep(1), shift, true);
          continue;
        case KeyCode.Home:
          _text.MoveTo(_map.LineStart(caret), shift);
          continue;
        case KeyCode.End:
          _text.MoveTo(_map.LineEnd(caret), shift);
          continue;
      }
      if (command)
      {
        switch (_event.keyCode)
        {
          case KeyCode.A:
            _text.SelectAll();
            break;
          case KeyCode.C:
            _text.Copy();
            break;
          case KeyCode.X:
            if (_text.Copy())
              _text.DeleteSelection();
            break;
          case KeyCode.V:
            _text.Paste();
            break;
          case KeyCode.Z:
            if (shift)
              _text.Redo();
            else
              _text.Undo();
            break;
          case KeyCode.Y:
            _text.Redo();
            break;
        }
        continue;
      }
      char c = _event.character;
      if (c == '\r' || c == '\n' || c == '\t' || c == 0 || char.IsControl(c))
        continue;
      _text.Type(c.ToString());
    }
  }

  // While a popover is open: Enter and Esc close it, and with a formula open
  // the keyboard types into it like the keypad. Other keys go back to the
  // text; after a value, typing continues behind it.
  private bool HandlePopoverKey(bool command)
  {
    if (command || !_host.AnyOpen)
      return false;
    KeyCode code = _event.keyCode;
    if (code == KeyCode.Escape || code == KeyCode.Return || code == KeyCode.KeypadEnter)
    {
      _host.CloseOpen();
      _host.Render();
      return true;
    }
    if (_formulas.Editing.Active && KeypadViewModel.TryKeyboard(_event, out char key))
    {
      _formulas.KeypadPress(key);
      return true;
    }
    if (_formulas.Field != null)
      return true;
    bool chip = _formulas.ChipOpen(out _);
    _host.CloseOpen();
    if (chip)
      _text.Anchor = _text.Caret = Mathf.Max(_text.Anchor, _text.Caret);
    _host.Render();
    return false;
  }

  private void MoveHorizontally(int direction, bool extend, bool word, bool line)
  {
    int caret = _text.Caret;
    int target;
    if (_text.HasSelection && !extend && !word && !line)
      target = direction < 0 ? Mathf.Min(_text.Anchor, caret) : Mathf.Max(_text.Anchor, caret);
    else if (line)
      target = direction < 0 ? _map.LineStart(caret) : _map.LineEnd(caret);
    else if (word)
      target = direction < 0 ? _text.Document.WordStart(caret) : _text.Document.WordEnd(caret);
    else
      target = caret + direction;
    _text.MoveTo(target, extend);
  }

  // The item on the next line up or down, as near the remembered x as it gets.
  private int LineStep(int direction)
  {
    _map.CaretGeometry(_text.Caret, out float x, out int line);
    if (float.IsNaN(_text.GoalX))
      _text.GoalX = x;
    int target = line + direction;
    if (target < 0)
      return 0;
    if (target >= _map.Lines)
      return _text.Document.Count;
    _map.LineExtent(target, out float top, out float bottom);
    return _map.IndexAtLocal(new Vector2(_text.GoalX, (top + bottom) * .5f));
  }

  /// <summary>Follows an input method's composition; a formula takes keys, not IME text.</summary>
  public void FollowComposition()
  {
    string composition = _formulas.Editing.Active ? "" : Input.compositionString ?? "";
    if (composition == _text.Composition)
      return;
    if (composition.Length > 0 && _text.HasSelection)
      _text.DeleteSelection();
    _text.Composition = composition;
    _host.Render();
  }

  /// <summary>The input method's window follows the caret.</summary>
  public void UpdateImePosition()
  {
    _map.CaretGeometry(_text.Caret, out float x, out int line);
    _map.LineExtent(line, out _, out float bottom);
    Vector2 screen = RectTransformUtility.WorldToScreenPoint(
      null,
      _preview.Text.rectTransform.TransformPoint(new Vector3(x, bottom, 0))
    );
    Input.compositionCursorPos = new Vector2(screen.x, Screen.height - screen.y);
  }
}
