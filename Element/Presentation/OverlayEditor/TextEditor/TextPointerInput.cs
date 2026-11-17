using Element.Domain.Overlay.Formula;
using Element.Domain.Overlay.Text;
using Element.Presentation.OverlayEditor.Parts;
using Element.Presentation.OverlayEditor.Parts.EditorFrame;
using Element.Presentation.OverlayEditor.Parts.Grow;
using Element.Presentation.Ui.ColorPicker;
using Element.Presentation.Ui.Popovers;
using UnityEngine;
using static Element.Presentation.Ui.Theme.UiLocale;

namespace Element.Presentation.OverlayEditor.TextEditor;

/// <summary>
/// The mouse in the text editor: a click puts the caret, a drag selects, a
/// double click selects a word, a click on a value opens its formula, and a
/// value under the pointer is named in a tooltip.
/// </summary>
internal sealed class TextPointerInput
{
  private readonly IEditorActions _editor;
  private readonly ITextEditorContext _context;
  private readonly TextDocumentEditing _text;
  private readonly TextFormulas _formulas;
  private readonly TextLayoutMap _map;
  private readonly TextColor _color;
  private readonly TextFonts _fonts;
  private readonly TextGradient _gradient;
  private readonly GrowFrom _grow;
  private readonly TextAnimate _animate;
  private readonly TextValueDrops _drops;
  private bool _dragging;
  private float _lastClick = -1;

  public TextPointerInput(
    IEditorActions editor,
    ITextEditorContext context,
    TextDocumentEditing text,
    TextFormulas formulas,
    TextLayoutMap map,
    TextColor color,
    TextFonts fonts,
    TextGradient gradient,
    GrowFrom grow,
    TextAnimate animate,
    TextValueDrops drops
  )
  {
    _editor = editor;
    _context = context;
    _text = text;
    _formulas = formulas;
    _map = map;
    _color = color;
    _fonts = fonts;
    _gradient = gradient;
    _grow = grow;
    _animate = animate;
    _drops = drops;
  }

  public void HandleMouse()
  {
    Vector2 screen = Input.mousePosition;
    if (_drops.Dragging || _gradient.Handles.Dragging)
      return;
    if (Input.GetMouseButtonDown(0))
    {
      UiColorPicker picker = _color.Picker;
      // Popovers, the bar, the picker and the gradient's handles handle their own clicks.
      if (
        _context.DockPanel.Contains(screen)
        || _grow.Contains(screen)
        || _fonts.Contains(screen)
        || _context.Inspector.Contains(screen)
        || _animate.Contains(screen)
        || PopoverPlacement.Contains(picker.Root, screen)
        || _gradient.Handles.Contains(screen)
      )
        return;
      bool popover = _context.AnyOpen;
      _editor.CloseOpen();
      if (!_context.InTextArea(screen))
      {
        if (popover)
          _editor.Render();
        return;
      }
      if (picker.Visible)
        picker.Hide();
      _animate.Hide();
      // A click on a value selects it whole and opens its formula.
      if (!Shift && _map.TryValueAt(screen, out int value))
      {
        _lastClick = Time.unscaledTime;
        _formulas.OpenChip(value);
        return;
      }
      int index = _map.IndexAt(screen);
      bool doubleClick = Time.unscaledTime - _lastClick < .35f;
      _lastClick = Time.unscaledTime;
      if (doubleClick)
      {
        OverlayTextDocument document = _text.Document;
        _text.Anchor = document.WordStart(Mathf.Min(document.Count, index + 1));
        _text.Caret = document.WordEnd(_text.Anchor);
        _dragging = false;
        _editor.Render();
        return;
      }
      _text.Caret = index;
      if (!Shift)
        _text.Anchor = index;
      _dragging = true;
      _text.GoalX = float.NaN;
      _editor.Render();
    }
    else if (_dragging && Input.GetMouseButton(0))
    {
      int index = _map.IndexAt(screen);
      if (index != _text.Caret)
      {
        _text.Caret = index;
        _editor.Render();
      }
    }
    else if (_dragging)
    {
      _dragging = false;
      _editor.Render();
    }
  }

  private static bool Shift => Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

  // ---- Naming the value under the pointer ----

  public void UpdateHover(Vector2 screen)
  {
    EditorFramePanel frame = _context.Frame;
    if (
      _drops.Dragging
      || _dragging
      || _context.DockPanel.Contains(screen)
      || _grow.Contains(screen)
      || _fonts.Contains(screen)
      || _animate.Contains(screen)
      || !_map.TryValueAt(screen, out int index)
    )
    {
      frame.HideTip();
      return;
    }
    OverlayTextItem item = _text.Document[index];
    OverlayFormula formula = OverlayFormula.Parse(item.Value);
    string words = formula.Describe(ValueSectionNames.ValueName);
    if (item.SizeFormula.Length > 0)
      words += L(" · 크기 ", " · size ") + OverlayFormula.Parse(item.SizeFormula).Describe(ValueSectionNames.ValueName);
    if (!_map.ItemTop(index, frame, out Vector2 anchor))
    {
      frame.HideTip();
      return;
    }
    frame.ShowTip(words, anchor);
  }
}
