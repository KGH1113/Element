using System;
using System.Text;
using Element.Domain.Overlay.Values;
using Element.Domain.Overlay.Variables;
using Element.Presentation.Overlay.Text;
using Element.Presentation.Overlay.Values;
using Element.Presentation.OverlayEditor.Parts;
using Element.Presentation.OverlayEditor.Parts.ColorPicker;
using Element.Presentation.OverlayEditor.Parts.Dock;
using Element.Presentation.OverlayEditor.Parts.Dock.Line;
using Element.Presentation.OverlayEditor.Parts.EditorFrame;
using Element.Presentation.OverlayEditor.Parts.Formula;
using Element.Presentation.OverlayEditor.Parts.Grow;
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
    GrowKey = "grow",
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
  private TextAnimate _animate;
  private TextGradient _gradient;
  private GrowFrom _grow;
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

  /// <summary>The fields the dock can open besides values.</summary>
  public void Use(TextSize size, TextColor color, TextGradient gradient, TextAnimate animate, GrowFrom grow)
  {
    _size = size;
    _color = color;
    _gradient = gradient;
    _animate = animate;
    _grow = grow;
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
    if (_fieldSlot == GrowKey)
      return _animate.Slot;
    if (_fieldSlot == GrowFrom.Key)
      return _grow.Slot;
    if (ChipOpen(out int index))
      return ChipSlot(index);
    // With nothing else open, the open color picker's color is what the dock types.
    return _fieldSlot == null && _color.CasesOpen ? _color.Cases.Slot : null;
  }

  public bool ChipOpen(out int index) => _text.SelectedValue(out index) && index == _chipPopover;

  public FormulaSlot ChipSlot(int index) =>
    new(
      "value:" + index,
      OverlayValueType.Any,
      () => OverlayVariable.Parse(_text.Document[index].Value),
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
  public bool Edits(string key, out OverlayVariable typed)
  {
    bool active = _fieldSlot == key;
    typed = active && _editing.Active ? _editing.Root : null;
    return active;
  }

  // ---- Opening and closing ----

  // A second click on the same number closes its popover.
  public void Activate(string key)
  {
    bool open = _fieldSlot == key;
    PutCompactPickerAway();
    _editor.CloseOpen();
    if (!open)
      _fieldSlot = key;
    _editor.Render();
  }

  // A solid color's picker stands where the keypad is: a number or a value
  // opened in the dock puts it away first, before it takes its numbers with it.
  private void PutCompactPickerAway()
  {
    if (_color.Picker.Visible && _color.Picker.InCompactPlace)
      _color.Picker.Hide();
  }

  // A click on a value selects it whole and opens its formula beside it; the column's open tool goes on with it.
  public void OpenChip(int index)
  {
    PutCompactPickerAway();
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
  /// Tells the dock what it edits: the field or value open in it, its name,
  /// whose it is and its result; nothing while none is open.
  /// </summary>
  public void Refresh(InputDockViewModel dock)
  {
    string owner =
      _fieldSlot != null ? _fieldSlot
      : ChipOpen(out int chip) ? "value:" + chip
      : _color.CasesOpen ? ColorCases.Key
      : null;
    string name =
      _gradient.Name(_fieldSlot)
      ?? (
        _fieldSlot == SizeKey ? L("크기", "Size")
        : _fieldSlot == GrowKey ? L("바뀔 때 애니메이션", "Animate on change")
        : _fieldSlot == OpacityKey ? L("불투명도", "Opacity")
        : _fieldSlot == GrowFrom.Key ? L("기준점", "Grow from")
        : owner == ColorCases.Key ? _color.OwnTitle
        : L("고른 값", "Picked value")
      );
    // A look is somebody's: the whole text's, the selected letters' or the picked value's. The grow point is the element's.
    bool look = (_fieldSlot != null && _fieldSlot != GrowFrom.Key) || owner == ColorCases.Key;
    dock.Show(owner, name, DockResult(), look ? LookOwner() + " ›" : null);
  }

  /// <summary>
  /// Whose look the column's fields hold: the value picked in the text (by
  /// what it says), the letters selected (in quotes), or the whole text.
  /// </summary>
  public string LookOwner()
  {
    _text.Range(out int start, out int end);
    if (!_text.HasSelection || start >= end)
      return L("글 전체", "Whole text");
    if (end - start == 1 && _text.Document[start].IsValue)
      return Short(ValueWords(OverlayVariable.Parse(_text.Document[start].Value)), 16);
    return "“" + Short(_text.Document.PlainText(start, end, _ => ""), 8) + "”";
  }

  // What a value in the text says: its pieces as the line names them.
  private string ValueWords(OverlayVariable value)
  {
    _builder.Clear();
    for (int i = 0; i < value.Count; i++)
      _builder.Append(i > 0 ? " " : "").Append(LineViewModel.Label(value[i]));
    return _builder.ToString();
  }

  private static string Short(string words, int most) =>
    words.Length > most ? words.Substring(0, most - 1) + "…" : words;

  // The result of what the dock types, as the text or the field will show it.
  private string DockResult()
  {
    if (!_editing.Active)
      return null;
    if (_editing.Slot.Key == ColorCases.Key)
      return _color.Cases.Result(_values.WithSamples);
    OverlayVariable formula = _editing.Root;
    if (formula.IsEmpty || _editing.Problem != null)
      return null;
    if (_fieldSlot == GrowFrom.Key)
      return _grow.Result(_values.WithSamples, formula);
    if (_fieldSlot == GrowKey)
      return formula.TryBool(_values.WithSamples, out bool grows)
        ? grows
          ? FormulaWords.Current.Yes
          : FormulaWords.Current.No
        : null;
    if (_fieldSlot == null)
    {
      // One number or some words typed by hand read the same as their result.
      if (formula.Count == 1 && formula[0].Kind is OverlayPieceKind.Number or OverlayPieceKind.Text)
        return null;
      _builder.Clear();
      OverlayFormulaText.Append(_builder, formula, _values, true);
      return _builder.Length > 0 ? _builder.ToString() : null;
    }
    return FormulaFieldView.Number(Result(formula))
      + (
        _fieldSlot == OpacityKey || _fieldSlot == PositionKey ? "%"
        : _fieldSlot == AngleKey ? "°"
        : " px"
      );
  }

  /// <summary>What <paramref name="formula"/> works out to with the editor's examples; <paramref name="fallback"/> when nothing (no case holds).</summary>
  public double Result(OverlayVariable formula, double fallback = double.NaN) =>
    formula.IsPlainNumber(out double plain) ? plain
    : formula.TryNumber(_values.WithSamples, out double result, out _) ? result
    : fallback;

  /// <summary>A number shows its result; one worked out from a formula reads in the accent.</summary>
  public void Show(FormulaFieldView field, string key, Func<OverlayVariable> read)
  {
    bool active = Edits(key, out OverlayVariable typed);
    OverlayVariable formula = typed ?? read();
    bool computed = !formula.IsEmpty && !formula.IsPlainNumber(out _);
    field.Set(formula.IsEmpty ? "—" : FormulaFieldView.Number(Result(formula)), computed, active);
  }

  // Leaves the open field; a number out of its range was already brought into it.
  private void Finish()
  {
    if (_fieldSlot == null)
      return;
    _fieldSlot = null;
    _editing.Deactivate();
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

  /// <summary>Backspace on the keyboard: one step of what was put in is taken back.</summary>
  public void Erase()
  {
    if (_context.Frame == null)
      return;
    _editing.Erase();
    _editor.Render();
  }

  /// <summary>
  /// Enter: what is open is done, from however deep inside it, once it fits
  /// its field. Until then it is refused, and the dock goes out to where
  /// what is in the way shows.
  /// </summary>
  public void FinishEditing()
  {
    if (_editing.Active && _editing.Problem != null)
    {
      _editing.FaceProblem();
      _editor.Refuse();
      return;
    }
    // Finishing a color puts its picker away.
    if (_fieldSlot == null && !ChipOpen(out _) && _color.Picker.Visible)
      _color.Picker.Hide();
    _editor.CloseOpen();
    _editor.Render();
  }

  /// <summary>Back: one step out of the slot the dock is in; false when it is in none.</summary>
  public bool LeaveSlot()
  {
    if (!_editing.Active || !_editing.Leave())
      return false;
    _editor.Render();
    return true;
  }

  /// <summary>Straight into another slot of the condition the dock is inside.</summary>
  public void HopSlot(int slot)
  {
    if (_editing.Active && _editing.Hop(slot))
      _editor.Render();
  }

  /// <summary>The dock goes into slot <paramref name="slot"/> of the picked condition.</summary>
  public void EnterSlot(int slot)
  {
    if (!_editing.Active || !_editing.CanEnter(slot))
      return;
    _editing.Enter(slot);
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
    // 그라데이션 makes the open color a gradient; its stops are then picked in the picker.
    if (value.Id == OverlayValueId.MakeGradient)
    {
      if (_color.CasesOpen)
        _color.Picker.Gradient.MakeGradient();
      _editor.Render();
      return;
    }
    // A color always goes to the open picker's color, whatever else the dock types.
    if (value.IsColor && _color.CasesOpen && _editing.Slot?.Key != ColorCases.Key)
    {
      _editor.CloseTyping();
      if (_editing.Drop(_color.Cases.Slot, value) == FormulaDropResult.Refused)
        Refuse(value);
      _editor.Render();
      return;
    }
    // A color can't go in the text itself.
    if (!_editing.Active && value.IsColor)
    {
      _editor.Refuse();
      return;
    }
    if (_editing.Active)
    {
      if (_editing.Drop(_editing.Slot, value) == FormulaDropResult.Refused)
        Refuse(value);
    }
    else if (value.IsMaker)
      InsertMade(value);
    else
    {
      _text.InsertValue(value, Mathf.Min(_text.Anchor, _text.Caret), Mathf.Max(_text.Anchor, _text.Caret));
      _text.Anchor = _text.Caret;
    }
    _editor.Render();
  }

  // Something made on the spot goes in at the caret and opens in the dock to
  // be filled: a number, words, true or false, a condition or a group.
  private void InsertMade(OverlayValueDefinition value)
  {
    OverlayPiece piece = FormulaEditing.PieceFor(value);
    int start = Mathf.Min(_text.Anchor, _text.Caret);
    _text.InsertFormula(new OverlayVariable(piece).Serialize(), start, Mathf.Max(_text.Anchor, _text.Caret));
    OpenChip(start);
    Sync();
  }

  /// <summary><paramref name="value"/> can't go where it was put.</summary>
  public void Refuse(OverlayValueDefinition value) => _editor.Refuse();
}
