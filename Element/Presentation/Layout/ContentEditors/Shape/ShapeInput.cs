using Element.Configuration.Overlay;
using Element.Presentation.Layout.ContentEditors.Shell;
using Element.Presentation.Ui.ColorPicker;
using Element.Presentation.Ui.Popovers;
using UnityEngine;

namespace Element.Presentation.Layout.ContentEditors.Shape;

/// <summary>
/// Keys and clicks in the shape editor: ⌘/Ctrl+Enter finishes, ⌘/Ctrl+Z
/// undoes, Enter and Esc close what is open, the keyboard types like the
/// keypad, arrows move a picked gradient color, and a click on the empty
/// dim closes what is open.
/// </summary>
internal sealed class ShapeInput
{
  private readonly IShapeEditorHost _host;
  private readonly ShapeNumbers _numbers;
  private readonly ShapeColors _colors;
  private readonly ShapeGradient _gradient;
  private readonly ShapeMeasures _measures;
  private readonly ShapeFieldList _fields;
  private readonly ShapeValueDrops _drops;
  private readonly OverlayGrowFrom _grow;
  private readonly OverlayEditorHistory<OverlayShapeSettings> _history;
  private readonly Event _event = new();

  public ShapeInput(
    IShapeEditorHost host,
    ShapeNumbers numbers,
    ShapeColors colors,
    ShapeGradient gradient,
    ShapeMeasures measures,
    ShapeFieldList fields,
    ShapeValueDrops drops,
    OverlayGrowFrom grow,
    OverlayEditorHistory<OverlayShapeSettings> history
  )
  {
    _host = host;
    _numbers = numbers;
    _colors = colors;
    _gradient = gradient;
    _measures = measures;
    _fields = fields;
    _drops = drops;
    _grow = grow;
    _history = history;
  }

  public void HandleKeys()
  {
    bool mac = EditorKeys.Mac;
    while (_host.Screen != null && EditorKeys.NextKeyDown(_event))
    {
      if (EditorKeys.Command(_event, mac))
      {
        if (EditorKeys.IsEnter(_event.keyCode))
        {
          _host.Close(true, true);
          return;
        }
        if (_event.keyCode == KeyCode.Z)
        {
          if (_event.shift)
            _history.Redo();
          else
            _history.Undo();
        }
        else if (_event.keyCode == KeyCode.Y)
          _history.Redo();
        continue;
      }
      KeyCode code = _event.keyCode;
      UiColorPicker picker = _colors.Picker;
      if (code == KeyCode.Escape)
      {
        bool finishing = !picker.Visible && !_host.AnyOpen;
        _host.Escape();
        if (finishing)
          return;
        continue;
      }
      // Enter closes what is open.
      if (EditorKeys.IsEnter(code))
      {
        if (picker.Visible)
          picker.Hide();
        else if (_host.AnyOpen)
          _host.CloseOpen();
        _host.Render();
        continue;
      }
      if (_gradient.Key(code, _event.shift, _numbers.Editing.Active))
        continue;
      if (_numbers.Editing.Active && OverlayKeypadModel.TryKeyboard(_event, out char key))
        _numbers.KeypadPress(key);
    }
  }

  // A click on the empty dim closes what is open.
  public void HandleMouse()
  {
    if (_drops.Dragging || _host.Dock.Dragging || _gradient.Handles.Dragging || !Input.GetMouseButtonDown(0))
      return;
    Vector2 screen = Input.mousePosition;
    if (OverControl(screen))
      return;
    UiColorPicker colors = _colors.Picker;
    bool picker = colors.Visible;
    if (picker)
      colors.Hide();
    if (!_host.AnyOpen && !picker)
      return;
    _host.CloseOpen();
    _host.Render();
  }

  private bool OverControl(Vector2 screen)
  {
    if (
      _host.Bar.Contains(screen)
      || _host.Dock.Contains(screen)
      || _grow.Contains(screen)
      || PopoverPlacement.Contains(_colors.Picker.Root, screen)
      || _gradient.Handles.Contains(screen)
      || _host.Screen.ToFrame(screen).y < OverlayEditorFrame.TopBar
    )
      return true;
    _fields.Collect();
    for (int i = 0; i < _fields.Count; i++)
      if (_fields[i].Contains(screen))
        return true;
    return _measures.ArcAt(screen, false) >= 0;
  }
}
