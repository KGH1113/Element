using Element.Domain.Overlay.Text;
using Element.Domain.Overlay.Variables;
using Element.Presentation.OverlayEditor.Parts;
using Element.Presentation.OverlayEditor.Parts.EditorFrame;
using Element.Presentation.OverlayEditor.Parts.Formula;
using Element.Presentation.Ui.ColorPicker;
using Element.Presentation.Ui.Popovers;
using ElementUI;
using UnityEngine;
using static Element.Presentation.Ui.Theme.UiLocale;

namespace Element.Presentation.OverlayEditor.TextEditor;

/// <summary>
/// The mouse in the text editor: a click puts the caret, a drag selects, a
/// double click selects a word, a click on a value opens its formula, and a
/// value under the pointer is named in a tooltip. None of it closes the
/// column's open tool: the tool goes on with the new selection.
/// </summary>
internal sealed class TextPointerInput
{
  private readonly ITextEditorActions _editor;
  private readonly ITextEditorContext _context;
  private readonly TextDocumentEditing _text;
  private readonly TextFormulas _formulas;
  private readonly TextLayoutMap _map;
  private readonly TextColor _color;
  private readonly TextFonts _fonts;
  private readonly TextGradient _gradient;
  private bool _dragging;
  private float _lastClick = -1;
  private int _clicks;

  public TextPointerInput(
    ITextEditorActions editor,
    ITextEditorContext context,
    TextDocumentEditing text,
    TextFormulas formulas,
    TextLayoutMap map,
    TextColor color,
    TextFonts fonts,
    TextGradient gradient
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
  }

  public void HandleMouse()
  {
    Vector2 screen = Input.mousePosition;
    if (DragSession.Active || _gradient.Handles.Dragging)
      return;
    if (Input.GetMouseButtonDown(0))
    {
      UiColorPicker picker = _color.Picker;
      // Popovers, the bar, the picker and the gradient's handles handle their own clicks.
      if (
        _context.DockPanel.Contains(screen)
        || _fonts.Contains(screen)
        || _context.Inspector.Contains(screen)
        || PopoverPlacement.Contains(picker.Root, screen)
        || _gradient.Handles.Contains(screen)
      )
        return;
      if (!_context.InTextArea(screen))
      {
        bool popover = _context.AnyOpen;
        _editor.CloseOpen();
        if (popover)
          _editor.Render();
        return;
      }
      // The column's open tool stays and goes on with what the click selects.
      _editor.CloseTyping();
      // A click on a value selects it whole and opens its formula.
      if (!Shift && _map.TryValueAt(screen, out int value))
      {
        _lastClick = Time.unscaledTime;
        // The open font list goes on with the value: it is picked for its font, its own formula stays shut.
        if (_fonts.Visible)
        {
          _text.Select(value, value + 1);
          _text.GoalX = float.NaN;
          _editor.Render();
          return;
        }
        _formulas.OpenChip(value);
        return;
      }
      int index = _map.IndexAt(screen);
      bool again = Time.unscaledTime - _lastClick < .35f;
      _lastClick = Time.unscaledTime;
      _clicks = again ? _clicks + 1 : 1;
      if (_clicks >= 2)
      {
        // Twice a word, three times the whole 글자 value, never past it.
        OverlayTextDocument document = _text.Document;
        if (_clicks >= 3 && OverlayTextRuns.RunOfCaret(document, index, out int start, out int end))
          _text.Select(start, end);
        else
        {
          int word = document.WordStart(Mathf.Min(document.Count, index + 1));
          _text.Select(word, document.WordEnd(word));
        }
        _dragging = false;
        _editor.Render();
        return;
      }
      if (Shift)
        _text.Select(_text.Anchor, index);
      else
        _text.Select(index, index);
      _dragging = true;
      _text.GoalX = float.NaN;
      _editor.Render();
    }
    else if (_dragging && Input.GetMouseButton(0))
    {
      int index = OverlayTextRuns.Reach(_text.Document, _text.Anchor, _map.IndexAt(screen));
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

  /// <summary>The value under the pointer as of the last <see cref="UpdateHover"/>; -1 for none.</summary>
  public int HoveredValue { get; private set; } = -1;

  public void UpdateHover(Vector2 screen)
  {
    HoveredValue = -1;
    EditorFramePanel frame = _context.Frame;
    if (
      DragSession.Active
      || _dragging
      || _context.DockPanel.Contains(screen)
      || _fonts.Contains(screen)
      || !_map.TryValueAt(screen, out int index)
    )
    {
      frame.HideTip();
      return;
    }
    HoveredValue = index;
    OverlayTextItem item = _text.Document[index];
    OverlayVariable formula = OverlayVariable.Parse(item.Value);
    OverlayVariableWords said = FormulaWords.Current;
    string words = formula.Describe(ValueSectionNames.ValueName, said);
    if (item.SizeFormula.Length > 0)
      words +=
        L(" · 크기 ", " · size ") + OverlayVariable.Parse(item.SizeFormula).Describe(ValueSectionNames.ValueName, said);
    if (!_map.ItemTop(index, frame, out Vector2 anchor))
    {
      frame.HideTip();
      return;
    }
    frame.ShowTip(words, anchor);
  }
}
