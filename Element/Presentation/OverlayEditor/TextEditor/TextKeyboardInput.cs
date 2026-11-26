using Element.Presentation.OverlayEditor.Parts;
using Element.Presentation.OverlayEditor.Parts.Dock.Keypad;
using UnityEngine;

namespace Element.Presentation.OverlayEditor.TextEditor;

/// <summary>
/// Keys in the text editor: typing (with an input method's composition),
/// moving the caret by character, word and line, selecting, deleting,
/// the clipboard, undo, and ⌘/Ctrl+Enter or Esc to finish. While the dock
/// has something open, the keys are the dock's: the keyboard types like the
/// keypad, Esc steps back out of a condition's slot and then closes what is
/// open, and nothing reaches the text.
/// </summary>
internal sealed class TextKeyboardInput
{
  private readonly ITextEditorActions _editor;
  private readonly ITextEditorContext _context;
  private readonly TextDocumentEditing _text;
  private readonly TextFormulas _formulas;
  private readonly TextLayoutMap _map;
  private readonly TextPreview _preview;
  private readonly Event _event = new();

  public TextKeyboardInput(
    ITextEditorActions editor,
    ITextEditorContext context,
    TextDocumentEditing text,
    TextFormulas formulas,
    TextLayoutMap map,
    TextPreview preview
  )
  {
    _editor = editor;
    _context = context;
    _text = text;
    _formulas = formulas;
    _map = map;
    _preview = preview;
  }

  public void HandleKeys()
  {
    while (_context.Frame != null && EditorKeys.NextKeyDown(_event))
      if (!Key(_event))
        return;
  }

  /// <summary>One key press, as the editor takes it; false once it closed the editor, so no later key is read.</summary>
  public bool Key(Event key)
  {
    bool mac = EditorKeys.Mac;
    bool command = EditorKeys.Command(key, mac);
    bool word = EditorKeys.Word(key, mac);
    bool shift = key.shift;
    if (HandleDockKey(key, command))
      return _context.Frame != null;
    int caret = _text.Caret;
    switch (key.keyCode)
    {
      case KeyCode.Escape:
        _editor.Escape();
        return false;
      case KeyCode.Return:
      case KeyCode.KeypadEnter:
        if (command)
        {
          _editor.Close(true, true);
          return false;
        }
        if (_text.Composition.Length == 0)
          _text.Type("\n");
        return true;
      case KeyCode.Backspace:
        if (_text.Composition.Length > 0)
          return true;
        if (_text.HasSelection)
          _text.DeleteSelection();
        else if (caret > 0)
          _text.DeleteRange(
            word ? _text.Document.WordStart(caret)
              : command ? _map.LineStart(caret)
              : caret - 1,
            caret
          );
        return true;
      case KeyCode.Delete:
        if (_text.HasSelection)
          _text.DeleteSelection();
        else if (caret < _text.Document.Count)
          _text.DeleteRange(caret, word ? _text.Document.WordEnd(caret) : caret + 1);
        return true;
      case KeyCode.LeftArrow:
        MoveHorizontally(-1, shift, word, command);
        return true;
      case KeyCode.RightArrow:
        MoveHorizontally(1, shift, word, command);
        return true;
      case KeyCode.UpArrow:
        _text.MoveTo(command ? 0 : LineStep(-1), shift, true);
        return true;
      case KeyCode.DownArrow:
        _text.MoveTo(command ? _text.Document.Count : LineStep(1), shift, true);
        return true;
      case KeyCode.Home:
        _text.MoveTo(_map.LineStart(caret), shift);
        return true;
      case KeyCode.End:
        _text.MoveTo(_map.LineEnd(caret), shift);
        return true;
    }
    if (command)
    {
      switch (key.keyCode)
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
      return true;
    }
    char c = key.character;
    if (c == '\r' || c == '\n' || c == '\t' || c == 0 || char.IsControl(c))
      return true;
    _text.Type(c.ToString());
    return true;
  }

  // While the dock has something open (a number, a color, the font list, a
  // value of the text), the keys are the dock's: Esc steps back out of a
  // condition's slot and then closes what is open, Enter closes it, and the
  // keyboard types like the keypad. Nothing else falls through to the text,
  // where it would type over or delete what is selected, the very thing
  // being edited. Only a value of the text itself, not gone into, lets
  // typing go on behind it.
  private bool HandleDockKey(Event key, bool command)
  {
    if (command || !_context.DockOpen)
      return false;
    KeyCode code = key.keyCode;
    if (code == KeyCode.Escape)
    {
      if (!_formulas.LeaveSlot())
        CloseDock();
      return true;
    }
    if (code == KeyCode.Return || code == KeyCode.KeypadEnter)
    {
      CloseDock();
      return true;
    }
    if (_formulas.Editing.Active && KeypadViewModel.TryKeyboard(key, _formulas.Editing, out char typed))
    {
      _formulas.KeypadPress(typed);
      return true;
    }
    if (!_formulas.ChipOpen(out _) || _formulas.Editing.Depth > 0)
      return true;
    _editor.CloseTyping();
    _text.Anchor = _text.Caret = Mathf.Max(_text.Anchor, _text.Caret);
    _editor.Render();
    return false;
  }

  // What the dock has open closes: a number or a value being typed first, else the picker or the font list.
  private void CloseDock()
  {
    if (_formulas.Open)
      _editor.CloseTyping();
    else if (_context.AnyOpen)
      _editor.CloseOpen();
    else
      _editor.Escape();
    _editor.Render();
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
    _editor.Render();
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
