using System;
using System.Text;
using Element.Domain.Overlay.Formula;
using Element.Domain.Overlay.Values;
using Element.Presentation.Overlay.Text;
using Element.Presentation.Overlay.Values;
using Element.Presentation.OverlayEditor.Parts;
using Element.Presentation.OverlayEditor.Parts.ColorPicker;
using Element.Presentation.OverlayEditor.Parts.Dock;
using Element.Presentation.OverlayEditor.Parts.EditorFrame;
using Element.Presentation.OverlayEditor.Parts.Formula;
using Element.Presentation.OverlayEditor.Parts.IfTool;
using UnityEngine;
using static Element.Presentation.Ui.Theme.UiLocale;

namespace Element.Presentation.OverlayEditor.TextEditor;

/// <summary>
/// What the input dock types into: a clicked number of the bar or the
/// picker, or a clicked value in the text while it stays selected. Holds the
/// formula being typed, points the dock at it, and says when a typed number
/// had to be brought into range.
/// </summary>
internal sealed class TextFormulas
{
  public const string SizeKey = "size",
    OpacityKey = "opacity",
    PositionKey = "position",
    AngleKey = "angle";

  private readonly ITextEditorActions _editor;
  private readonly ITextEditorContext _context;
  private readonly TextDocumentEditing _text;
  private readonly OverlayValues _values;
  private readonly FormulaEditing _editing = new();
  private readonly StringBuilder _builder = new(128);
  private TextSize _size;
  private TextColor _color;
  private TextGradient _gradient;
  private TextIf _if;
  private string _fieldSlot;

  // The value whose popover is open, while it stays selected; -1 for none.
  private int _chipPopover = -1;

  public TextFormulas(
    ITextEditorActions editor,
    ITextEditorContext context,
    TextDocumentEditing text,
    OverlayValues values
  )
  {
    _editor = editor;
    _context = context;
    _text = text;
    _values = values;
  }

  public FormulaEditing Editing => _editing;

  /// <summary>The key of the number open in the dock; null with none.</summary>
  public string Field => _fieldSlot;

  /// <summary>A number or a value is open in the dock.</summary>
  public bool Open => _fieldSlot != null || _chipPopover >= 0;

  /// <summary>The value being calculated in the dock, or -1.</summary>
  public int EditedValue => ChipOpen(out int index) ? index : -1;

  /// <summary>The numbers the dock can type besides values.</summary>
  public void Use(TextSize size, TextColor color, TextGradient gradient, TextIf textIf)
  {
    _size = size;
    _color = color;
    _gradient = gradient;
    _if = textIf;
  }

  /// <summary>Nothing open, as when the editor opens.</summary>
  public void Reset()
  {
    _fieldSlot = null;
    _chipPopover = -1;
    _editing.Deactivate();
  }

  // ---- Which formula the keypad edits ----

  // A clicked number field, or the clicked value while it stays selected.
  private FormulaSlot CurrentSlot()
  {
    FormulaSlot gradient = _gradient.Slot(_fieldSlot);
    if (gradient != null)
      return gradient;
    if (_fieldSlot == SizeKey)
      return _size.Slot;
    if (_fieldSlot == OpacityKey)
      return _color.OpacitySlot;
    if (TextIf.Owns(_fieldSlot))
      return _if.Slot(_fieldSlot);
    return ChipOpen(out int index) ? ChipSlot(index) : null;
  }

  public bool ChipOpen(out int index) => _text.SelectedValue(out index) && index == _chipPopover;

  public FormulaSlot ChipSlot(int index) =>
    new(
      "value:" + index,
      true,
      () => OverlayFormula.Parse(_text.Document[index].Value),
      formula =>
      {
        _text.RememberFormula("value:" + index);
        _text.Document.SetValue(index, formula.Serialize());
        if (formula.IsEmpty)
          _text.Caret = _text.Anchor = Mathf.Min(index, _text.Document.Count);
      }
    );

  /// <summary>
  /// Whether the dock edits <paramref name="key"/>; <paramref name="typed"/>
  /// is what it typed there so far while the keypad has it.
  /// </summary>
  public bool Edits(string key, out OverlayFormula typed)
  {
    bool active = _fieldSlot == key;
    typed = active && _editing.Active ? _editing.Working.Trimmed() : null;
    return active;
  }

  // ---- Opening and closing ----

  // A second click on the same number closes its popover.
  public void Activate(string key)
  {
    bool open = _fieldSlot == key;
    _editor.CloseOpen();
    if (!open)
      _fieldSlot = key;
    _editor.Render();
  }

  // A click on a value selects it whole and opens its formula beside it; the column's open tool goes on with it.
  public void OpenChip(int index)
  {
    _editor.CloseTyping();
    _text.Anchor = index;
    _text.Caret = index + 1;
    _chipPopover = index;
    _text.GoalX = float.NaN;
    _editor.Render();
  }

  /// <summary>Leaves the number or value being typed.</summary>
  public void Close()
  {
    Finish();
    _chipPopover = -1;
    _editing.Deactivate();
  }

  /// <summary>Points the formula at what is being edited, or lets it rest once that is gone.</summary>
  public void Sync()
  {
    _editing.Activate(CurrentSlot());
    if (_chipPopover >= 0 && !ChipOpen(out _))
    {
      _chipPopover = -1;
      _editing.Deactivate();
    }
  }

  /// <summary>
  /// Tells the dock what it edits: the number or value open in it, its name
  /// and result, or with nothing open what to do.
  /// </summary>
  public void Refresh(InputDockViewModel dock)
  {
    string owner =
      _fieldSlot != null ? _fieldSlot
      : ChipOpen(out int chip) ? "value:" + chip
      : null;
    string hint =
      _color.Hint()
      ?? _if.Hint()
      ?? L(
        "값을 누르면 커서 자리에 넣어요 · 글 속 값이나 크기를 누르면 여기서 계산해요",
        "Click a value to add it at the caret · Click a value in the text or a number to calculate here"
      );
    string name =
      _gradient.Name(_fieldSlot)
      ?? TextIf.Name(_fieldSlot)
      ?? (
        _fieldSlot == SizeKey ? L("글자 크기", "Text size")
        : _fieldSlot == OpacityKey ? L("불투명도", "Opacity")
        : L("값", "Value")
      );
    dock.Show(owner, name, DockResult(), hint);
  }

  // The result of what the dock types, as the text or the field will show it.
  private string DockResult()
  {
    if (!_editing.Active)
      return null;
    OverlayFormula formula = _editing.Working.Trimmed();
    if (formula.IsEmpty)
      return null;
    if (_fieldSlot == null)
    {
      _builder.Clear();
      OverlayFormulaText.Append(_builder, formula, _values, true);
      return _builder.Length > 0 ? _builder.ToString() : null;
    }
    // A number an If compares with is whatever its value counts in.
    if (TextIf.Owns(_fieldSlot))
      return IfWords.Number(Result(formula));
    return FormulaFieldView.Number(Result(formula))
      + (
        _fieldSlot == OpacityKey || _fieldSlot == PositionKey ? "%"
        : _fieldSlot == AngleKey ? "°"
        : " px"
      );
  }

  public double Result(OverlayFormula formula) =>
    formula.IsPlainNumber(out double plain) ? plain
    : formula.TryEvaluate(_values.WithSamples, out double result, out _) ? result
    : double.NaN;

  /// <summary>A number shows its result; one worked out from a formula reads in the accent.</summary>
  public void Show(FormulaFieldView field, string key, Func<OverlayFormula> read)
  {
    bool active = Edits(key, out OverlayFormula typed);
    OverlayFormula formula = typed ?? read();
    bool computed = !formula.IsEmpty && !formula.IsPlainNumber(out _);
    field.Set(formula.IsEmpty ? "—" : FormulaFieldView.Number(Result(formula)), computed, active);
  }

  // Leaves a number field, saying so when what was typed had to be brought into range.
  private void Finish()
  {
    if (_fieldSlot == null)
      return;
    string key = _fieldSlot;
    OverlayFormula typed = _editing.Working?.Trimmed();
    _fieldSlot = null;
    _editing.Deactivate();
    if (typed != null && typed.IsPlainNumber(out double number))
    {
      if (key == SizeKey && (number < TextSize.Minimum || number > TextSize.Maximum))
        _editor.Notice(L("글자 크기는 4–400px 사이로 맞췄어요", "Text size was kept between 4 and 400px"));
      else if (key == OpacityKey && OpacityFieldView.OutOfRange(typed))
        _editor.Notice(L("불투명도는 0–100% 사이로 맞췄어요", "Opacity was kept between 0 and 100%"));
      else if (key == PositionKey && (number < 0 || number > 100))
        _editor.Notice(L("색 위치는 0–100% 사이로 맞췄어요", "The color's position was kept between 0 and 100%"));
    }
  }

  // ---- The dock's keypad and values ----

  public void KeypadPress(char key)
  {
    if (_context.Frame == null)
      return;
    if (key == '\b')
      _editing.Backspace();
    else
      _editing.Press(key);
    _editor.Render();
  }

  /// <summary>Starting over is its own undo step.</summary>
  public void ClearFormula()
  {
    _editing.Clear();
    _text.BreakFormula();
    _editor.Render();
  }

  public void FinishEditing()
  {
    _editor.CloseOpen();
    _editor.Render();
  }

  /// <summary>
  /// A value picked in the dock joins or replaces the formula being typed;
  /// with none, it is added at the caret and typing goes on after it.
  /// </summary>
  public void PickValue(OverlayValueDefinition value)
  {
    if (_context.Frame == null)
      return;
    if (_editing.Active)
    {
      if (_editing.Drop(_editing.Slot, value) == FormulaDropResult.RefusedWords)
        RefuseWords(value);
    }
    else
    {
      _text.InsertValue(value, Mathf.Min(_text.Anchor, _text.Caret), Mathf.Max(_text.Anchor, _text.Caret));
      _text.Anchor = _text.Caret;
    }
    _editor.Render();
  }

  /// <summary>A value dropped on a number opens that number and calculates it with the value.</summary>
  public void DropOn(string key, OverlayValueDefinition value)
  {
    if (!value.IsNumeric)
    {
      RefuseWords(value);
      return;
    }
    if (_fieldSlot != key)
    {
      _editor.CloseOpen();
      _fieldSlot = key;
    }
    _editing.Drop(CurrentSlot(), value);
  }

  public void RefuseWords(OverlayValueDefinition value) =>
    _editor.Notice(
      L(
        ValueSectionNames.ValueName(value) + "은 글자라서 계산에 쓸 수 없어요. 글 안에 넣어 주세요.",
        ValueSectionNames.ValueName(value) + " is words, so it can't be calculated. Put it in the text instead."
      )
    );
}
