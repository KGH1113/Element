using Element.Configuration.Overlay;
using Element.Presentation.OverlayEditor.Parts;
using Element.Presentation.OverlayEditor.Parts.Dock.Keypad;
using Element.Presentation.OverlayEditor.Parts.EditorFrame;
using Element.Presentation.OverlayEditor.Parts.Grow;
using Element.Presentation.Ui.ColorPicker;
using Element.Presentation.Ui.Popovers;
using ElementUI;
using UnityEngine;

namespace Element.Presentation.OverlayEditor.ShapeEditor;

/// <summary>
/// Keys and clicks in the shape editor: ⌘/Ctrl+Enter finishes, ⌘/Ctrl+Z
/// undoes, Enter and Esc close what is open, the keyboard types like the
/// keypad, arrows move a picked gradient color, and a click on the empty
/// dim closes what is open.
/// </summary>
internal sealed class ShapeInput
{
  private readonly IShapeEditorActions _editor;
  private readonly IShapeEditorContext _context;
  private readonly ShapeNumbers _numbers;
  private readonly ShapeColors _colors;
  private readonly ShapeGradient _gradient;
  private readonly ShapeMeasures _measures;
  private readonly ShapeFieldList _fields;
  private readonly EditorHistory<OverlayShapeSettings> _history;
  private readonly Event _event = new();

  public ShapeInput(
    IShapeEditorActions editor,
    IShapeEditorContext context,
    ShapeNumbers numbers,
    ShapeColors colors,
    ShapeGradient gradient,
    ShapeMeasures measures,
    ShapeFieldList fields,
    EditorHistory<OverlayShapeSettings> history
  )
  {
    _editor = editor;
    _context = context;
    _numbers = numbers;
    _colors = colors;
    _gradient = gradient;
    _measures = measures;
    _fields = fields;
    _history = history;
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
    if (EditorKeys.Command(key, EditorKeys.Mac))
    {
      if (EditorKeys.IsEnter(key.keyCode))
      {
        _editor.Close(true, true);
        return false;
      }
      if (key.keyCode == KeyCode.Z)
      {
        if (key.shift)
          _history.Redo();
        else
          _history.Undo();
      }
      else if (key.keyCode == KeyCode.Y)
        _history.Redo();
      return true;
    }
    KeyCode code = key.keyCode;
    UiColorPicker picker = _colors.Picker;
    if (code == KeyCode.Escape)
    {
      // Esc steps back out of a condition's slot, then closes what is open, then finishes.
      _editor.Escape();
      return _context.Frame != null;
    }
    // Enter: what is open is done, refused while a number doesn't fit yet.
    if (EditorKeys.IsEnter(code))
    {
      if (picker.Visible || _context.AnyOpen)
        _editor.Finish();
      return true;
    }
    if (_gradient.Key(code, key.shift, _numbers.Editing.Active))
      return true;
    if (_numbers.Editing.Active && KeypadViewModel.TryKeyboard(key, _numbers.Editing, out char typed))
    {
      // Backspace takes back one step: a digit, a letter, or the value last put in.
      if (typed == '\b')
        _numbers.Erase();
      else
        _numbers.KeypadPress(typed);
    }
    return true;
  }

  // A click on the empty dim closes what is open.
  public void HandleMouse()
  {
    if (DragSession.Active || _gradient.Handles.Dragging || !Input.GetMouseButtonDown(0))
      return;
    Vector2 screen = Input.mousePosition;
    if (OverControl(screen))
      return;
    UiColorPicker colors = _colors.Picker;
    bool picker = colors.Visible;
    if (picker)
      colors.Hide();
    if (!_context.AnyOpen && !picker)
      return;
    _editor.CloseOpen();
    _editor.Render();
  }

  // The property column's space above the keypad, rows or not, is the column's.
  private bool InColumn(Vector2 frame) => frame.x >= _context.Inspector.Left - 8 && frame.y < _context.DockPanel.Top;

  private bool OverControl(Vector2 screen)
  {
    if (
      _context.Inspector.Contains(screen)
      || _context.DockPanel.Contains(screen)
      || PopoverPlacement.Contains(_colors.Picker.Root, screen)
      || _gradient.Handles.Contains(screen)
      || _context.Frame.ToFrame(screen).y < EditorFramePanel.TopBar
      || InColumn(_context.Frame.ToFrame(screen))
    )
      return true;
    _fields.Collect();
    for (int i = 0; i < _fields.Count; i++)
      if (_fields[i].Contains(screen))
        return true;
    return _measures.ArcAt(screen, false) >= 0;
  }
}
