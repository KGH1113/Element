using System;
using System.Collections.Generic;
using Element.Domain.Overlay.Values;
using Element.Presentation.OverlayEditor.Parts;
using Element.Presentation.OverlayEditor.Parts.EditorFrame;
using Element.Presentation.OverlayEditor.Parts.Formula;
using ElementUI;
using ElementUI.Ugui;
using UnityEngine;
using static Element.Presentation.Ui.Theme.UiLocale;

namespace Element.Presentation.OverlayEditor.TextEditor;

/// <summary>
/// Where a value carried out of the dock can land in the text editor, each
/// place answering for itself: the text (at an accent caret, or onto the
/// value whose formula waits for its next operand), the size row, the
/// picker's opacity, a color's position or its square, and the handles'
/// angle. A place that can't take a value says why when it is let go there.
/// ElementUI finds the place under the pointer, so whatever is drawn over a
/// place shields it. The dock's formula line is the dock's own place.
/// </summary>
internal sealed class TextValueDrops
{
  private readonly ITextEditorActions _editor;
  private readonly ITextEditorContext _context;
  private readonly TextDocumentEditing _text;
  private readonly TextFormulas _formulas;
  private readonly TextLayoutMap _map;
  private readonly TextMarks _marks;
  private readonly TextColor _color;
  private readonly TextGradient _gradient;
  private readonly TextArea _textArea;
  private readonly List<IDisposable> _places = new();

  public TextValueDrops(
    ITextEditorActions editor,
    ITextEditorContext context,
    TextDocumentEditing text,
    TextFormulas formulas,
    TextLayoutMap map,
    TextMarks marks,
    TextColor color,
    TextGradient gradient
  )
  {
    _editor = editor;
    _context = context;
    _text = text;
    _formulas = formulas;
    _map = map;
    _marks = marks;
    _color = color;
    _gradient = gradient;
    _textArea = new TextArea(this);
  }

  /// <summary>Where a carried value would go into the text; -1 while it isn't over the text.</summary>
  public int InsertIndex => _textArea.Hovering && !_textArea.Joining ? _textArea.Index : -1;

  /// <summary>A carried value is over the value whose formula waits for it.</summary>
  public bool Joining => _textArea.Hovering && _textArea.Joining;

  /// <summary>A value let go on the size row: it calculates the size with the value, or says why words can't.</summary>
  public void DropOnSize(OverlayValueDefinition value) => DropOn(TextFormulas.SizeKey, value);

  /// <summary>
  /// Once the editor's frame, picker and handles are built: the text and each
  /// number on them take values from now on.
  /// </summary>
  public void Build(EditorFramePanel frame)
  {
    Release();
    // Under everything else: what isn't another place, and is the text's space, is the text.
    _places.Add(UguiDrop.Destination(frame.Root, _textArea));
    _places.Add(Number(_color.Opacity.Field, TextFormulas.OpacityKey));
    _places.Add(Number(_gradient.Position, TextFormulas.PositionKey));
    _places.Add(Number(_gradient.Handles.Angle, TextFormulas.AngleKey));
    _places.Add(UguiDrop.Destination(_gradient.Handles.Root.gameObject, new Squares(this)));
  }

  /// <summary>The editor closes or is laid out afresh: its places go with what they were on.</summary>
  public void Release()
  {
    foreach (IDisposable place in _places)
      place.Dispose();
    _places.Clear();
    _textArea.Left();
  }

  private IDisposable Number(FormulaFieldView field, string key) =>
    UguiDrop.Destination<OverlayValueDefinition>(
      field.Root.gameObject,
      over: Numeric,
      drop: value => DropOn(key, value),
      targeted: field.SetDrop,
      refused: value => DropOn(key, value)
    );

  private static DropState Numeric(OverlayValueDefinition value) =>
    value.IsNumeric ? DropState.Accepts : DropState.Refuses;

  // Let go on a number: it is calculated with the value; words can't be, and it says so.
  private void DropOn(string key, OverlayValueDefinition value)
  {
    _formulas.DropOn(key, value);
    _editor.Render();
  }

  // ---- The text ----

  private sealed class TextArea : IDropDestination<OverlayValueDefinition>
  {
    private readonly TextValueDrops _drops;

    public TextArea(TextValueDrops drops) => _drops = drops;

    public bool Hovering { get; private set; }
    public bool Joining { get; private set; }
    public int Index { get; private set; } = -1;

    public DropState Over(OverlayValueDefinition value, Vector2 screen)
    {
      // The header, the column, the dock and the popovers are not the text's.
      if (!_drops._context.InTextArea(screen))
      {
        Left();
        return DropState.None;
      }
      FormulaEditing editing = _drops._formulas.Editing;
      // Onto the value whose formula waits for its next number or value.
      if (
        editing.Active
        && editing.Working.AwaitsOperand
        && _drops._formulas.ChipOpen(out int selected)
        && _drops._map.ItemContains(selected, screen)
      )
      {
        Show(selected, joining: true);
        return value.IsNumeric ? DropState.Accepts : DropState.Refuses;
      }
      Show(_drops._map.IndexAt(screen), joining: false);
      // A color colors a text; it can't be a part of one.
      return value.IsColor ? DropState.Refuses : DropState.Accepts;
    }

    public void Drop(OverlayValueDefinition value, Vector2 screen)
    {
      int index = Index;
      bool joining = Joining;
      Left();
      if (value.IsColor && !joining)
      {
        _drops._editor.Notice(
          L(
            "색은 글 안에 넣을 수 없어요. 글자 색을 열고 골라 주세요.",
            "A color can't go in the text. Open Text color and pick it there."
          )
        );
        return;
      }
      if (joining)
        _drops._formulas.Editing.Drop(_drops._formulas.ChipSlot(index), value);
      else
      {
        // A value dropped into the text lands there; typing goes on after it, and the column's tool stays.
        _drops._editor.CloseTyping();
        _drops._text.InsertValue(value, index, index);
        _drops._text.Anchor = _drops._text.Caret;
      }
      _drops._editor.Render();
    }

    // Words onto a number's formula: nothing joins, as when typed.
    public void Refused(OverlayValueDefinition value, Vector2 screen) => Left();

    public void Left()
    {
      if (!Hovering && Index < 0)
        return;
      Hovering = false;
      Joining = false;
      Index = -1;
      _drops.ShowMarks();
    }

    private void Show(int index, bool joining)
    {
      if (Hovering && Index == index && Joining == joining)
        return;
      Hovering = true;
      Index = index;
      Joining = joining;
      _drops.ShowMarks();
    }
  }

  private void ShowMarks() => _marks.Place(InsertIndex, Joining, _formulas.EditedValue);

  // ---- A gradient's squares, over the text ----

  // A square takes a value for its color's position; between the squares the
  // value goes on to the text under the handles, as if they weren't there.
  private sealed class Squares : IDropDestination<OverlayValueDefinition>
  {
    private readonly TextValueDrops _drops;
    private int _stop = -1;

    public Squares(TextValueDrops drops) => _drops = drops;

    private IDropDestination<OverlayValueDefinition> Text => _drops._textArea;

    public DropState Over(OverlayValueDefinition value, Vector2 screen)
    {
      _stop = _drops._gradient.Handles.SquareAt(screen);
      if (_stop < 0)
        return Text.Over(value, screen);
      Text.Left();
      return Numeric(value);
    }

    public void Drop(OverlayValueDefinition value, Vector2 screen)
    {
      if (_stop < 0)
      {
        Text.Drop(value, screen);
        return;
      }
      _drops._color.Picker.Gradient.SelectStop(_stop);
      _drops.DropOn(TextFormulas.PositionKey, value);
    }

    public void Refused(OverlayValueDefinition value, Vector2 screen)
    {
      if (_stop < 0)
        Text.Refused(value, screen);
      else
        Drop(value, screen);
    }

    public void Left()
    {
      _stop = -1;
      Text.Left();
    }
  }
}
