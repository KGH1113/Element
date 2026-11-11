using System;
using System.Collections.Generic;
using System.Text;
using Element.Domain.Overlay.Formula;
using Element.Domain.Overlay.Values;
using Element.Presentation.Ui.Popovers;
using Element.Presentation.Ui.Theme;
using Element.Presentation.Ui.Widgets;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static Element.Presentation.Ui.Theme.UiLocale;

namespace Element.Presentation.Layout.ContentEditors.Shell;

/// <summary>
/// Where formulas are typed: a wide dock along the bottom of an editor, always
/// there, with no card behind it. One full-width line shows what is edited,
/// its formula with values as chips, and the result; below it every value is
/// laid out by category beside a large keypad. With nothing being edited the
/// line says what to do and the keypad rests. Editors keep their preview above it.
/// </summary>
internal sealed class OverlayInputDock
{
  private const float LineHeight = 58,
    LineGap = 14,
    BottomGap = 24,
    ChipHeight = 30,
    ChipGap = 8,
    RowGap = 8,
    CategoryGap = 10,
    KeypadGap = 32;

  private readonly Action<OverlayValueDefinition, Vector2> _begin;
  private readonly Action<Vector2> _move;
  private readonly Action<Vector2> _end;
  private readonly float _width;
  private readonly RectTransform _line;
  private readonly TextMeshProUGUI _name;
  private readonly RectTransform _formulaViewport;
  private readonly TextMeshProUGUI _formula;
  private readonly Image _caret;
  private readonly TextMeshProUGUI _result;
  private readonly Image _underline;
  private readonly RectTransform _values;
  private readonly ScrollRect _scroll;
  private readonly List<Button> _chips = new();
  private readonly StringBuilder _builder = new(128);
  private int _drop;
  private bool _editing;

  public OverlayInputDock(
    UiFactory ui,
    Transform parent,
    float frameWidth,
    float frameHeight,
    TextMeshProUGUI measure,
    Func<bool> reduceMotion,
    Action<char> press,
    Action clear,
    Action done,
    Action<OverlayValueDefinition> pick,
    Action<OverlayValueDefinition, Vector2> begin,
    Action<Vector2> move,
    Action<Vector2> end
  )
  {
    _begin = begin;
    _move = move;
    _end = end;
    _width = Mathf.Min(1180, frameWidth - OverlayEditorFrame.Margin * 2);
    float height = LineHeight + LineGap + OverlayKeypad.Height;
    Top = frameHeight - BottomGap - height;
    Left = (frameWidth - _width) * .5f;
    Root = ui.Rect("Input dock", parent, Left, Top, _width, height);

    // The line: what is edited, its formula, and the result on the right.
    _line = ui.Rect("Formula line", Root, 0, 0, _width, LineHeight);
    Image hit = _line.gameObject.AddComponent<Image>();
    hit.color = Color.clear;
    _name = ui.Text("", _line, 0, 0, 10, LineHeight, 17, UiPalette.Muted);
    _name.textWrappingMode = TextWrappingModes.NoWrap;
    _name.overflowMode = TextOverflowModes.Ellipsis;
    _formulaViewport = ui.Rect("Formula", _line, 0, 0, 10, LineHeight);
    _formulaViewport.gameObject.AddComponent<RectMask2D>();
    _formula = ui.Text("", _formulaViewport, 0, 0, 10, LineHeight, 26);
    _formula.richText = true;
    _formula.textWrappingMode = TextWrappingModes.NoWrap;
    _formula.overflowMode = TextOverflowModes.Overflow;
    _caret = ui.Panel("Caret", _formulaViewport, 0, 14, 2, LineHeight - 28, Color.white, false);
    _result = ui.Text("", _line, 0, 0, 10, LineHeight, 22, UiPalette.SoftAccent);
    _result.textWrappingMode = TextWrappingModes.NoWrap;
    _result.alignment = TextAlignmentOptions.MidlineRight;
    _underline = ui.Panel("Underline", Root, 0, LineHeight, _width, 1, new Color(1, 1, 1, .16f), false);

    Keypad = new OverlayKeypad(ui, Root, press, clear, done, reduceMotion());
    Keypad.Place(_width - OverlayKeypad.Width, LineHeight + LineGap);

    // Every value at once: category names in a column, chips to their right.
    _values = ui.Rect("Values", Root, 0, LineHeight + LineGap, 10, OverlayKeypad.Height);
    _values.gameObject.AddComponent<RectMask2D>();
    Image valuesHit = _values.gameObject.AddComponent<Image>();
    valuesHit.color = Color.clear;
    _scroll = _values.gameObject.AddComponent<ScrollRect>();
    _scroll.horizontal = false;
    _scroll.movementType = ScrollRect.MovementType.Clamped;
    _scroll.scrollSensitivity = 34;
    _scroll.inertia = !reduceMotion();
    _scroll.viewport = _values;
    Content = ui.Rect("Chips", _values, 0, 0, 10, 10);
    _scroll.content = Content;
    BuildValues(ui, measure, reduceMotion(), pick);
    float valuesWidth = _width - OverlayKeypad.Width - KeypadGap;
    UiFactory.Place(_values, 0, LineHeight + LineGap, valuesWidth, OverlayKeypad.Height);
    LayOutValues(valuesWidth);
  }

  /// <summary>Where the dock starts, in frame units; previews stay above it.</summary>
  public float Top { get; }

  /// <summary>The keypad's column, in frame units, for what opens right above it.</summary>
  public Rect KeypadArea =>
    new(Left + _width - OverlayKeypad.Width, Top + LineHeight + LineGap, OverlayKeypad.Width, OverlayKeypad.Height);

  private float Left { get; }

  public RectTransform Root { get; }
  public RectTransform Content { get; }
  public OverlayKeypad Keypad { get; }
  public IReadOnlyList<Button> Chips => _chips;

  /// <summary>What the dock edits, such as "width" or "value:3"; null while nothing is.</summary>
  public string Owner { get; private set; }

  /// <summary>A value is being carried out of the dock.</summary>
  public bool Dragging { get; private set; }

  public string FormulaText => _formula.text;
  public string ResultText => _result.gameObject.activeSelf ? _result.text : "";
  public string HintText => Owner == null ? _name.text : "";

  /// <summary>What the dock edits from now on; null to rest.</summary>
  public void Edit(string owner) => Owner = owner;

  /// <summary>
  /// Shows <paramref name="name"/> and the formula <paramref name="editing"/>
  /// types, with <paramref name="result"/> (already in its unit) on the right;
  /// with nothing being edited, <paramref name="hint"/> says what to do.
  /// </summary>
  public void Refresh(OverlayFormulaEditing editing, string name, string result, string hint)
  {
    _editing = Owner != null && editing.Active;
    float x = 0;
    _name.text = _editing ? name + "  =" : hint;
    _name.color = _editing ? UiPalette.Muted : new Color(1, 1, 1, .5f);
    float nameWidth = Mathf.Min(_width, Mathf.Ceil(_name.GetPreferredValues(_name.text).x) + 4);
    UiFactory.Place(_name.rectTransform, x, 0, nameWidth, LineHeight);
    x += nameWidth + 12;
    _result.text = _editing && !string.IsNullOrEmpty(result) ? "= " + result : "";
    _result.gameObject.SetActive(_result.text.Length > 0);
    float resultWidth = _result.text.Length > 0 ? Mathf.Ceil(_result.GetPreferredValues(_result.text).x) + 4 : 0;
    UiFactory.Place(_result.rectTransform, _width - resultWidth, 0, resultWidth, LineHeight);
    float available = Mathf.Max(40, _width - resultWidth - 24 - x);
    UiFactory.Place(_formulaViewport, x, 0, available, LineHeight);
    _formula.text = _editing ? Describe(editing.Working) : "";
    if (_editing && editing.Working.IsEmpty)
    {
      _formula.text = L("숫자를 누르거나 값을 고르세요", "Type a number or pick a value");
      _formula.color = new Color(1, 1, 1, .32f);
    }
    else
      _formula.color = UiPalette.Foreground;
    // A long formula keeps its end in view, where the caret is.
    float textWidth = Mathf.Ceil(_formula.GetPreferredValues(_formula.text).x) + 2;
    float left = editing.Working != null && !editing.Working.IsEmpty ? Mathf.Min(0, available - textWidth - 6) : 0;
    UiFactory.Place(_formula.rectTransform, left, 0, Mathf.Max(textWidth, 10), LineHeight);
    float caretX = editing.Working != null && editing.Working.IsEmpty ? 0 : left + textWidth + 2;
    UiFactory.Place(_caret.rectTransform, caretX, 15, 2, LineHeight - 30);
    _caret.enabled = _editing;
    Keypad.Refresh(editing);
  }

  public void Blink(bool visible) => _caret.enabled = _editing && visible;

  public bool Contains(Vector2 screen) => PopoverPlacement.Contains(Root, screen);

  /// <summary>The formula line, where a dropped value joins the formula.</summary>
  public bool FormulaContains(Vector2 screen) => Owner != null && PopoverPlacement.Contains(_line, screen);

  /// <summary>0 nothing, 1 a dragged value can join here, -1 it can't.</summary>
  public void SetDrop(int state)
  {
    if (_drop == state)
      return;
    _drop = state;
    Color accent = UiPalette.SoftAccent;
    _underline.color =
      state > 0 ? accent
      : state < 0 ? UiPalette.Error
      : new Color(1, 1, 1, .16f);
  }

  // The formula as it reads: values as soft chips, operators spaced out.
  private string Describe(OverlayFormula formula)
  {
    _builder.Clear();
    for (int i = 0; i < formula.Count; i++)
    {
      OverlayFormulaToken token = formula[i];
      switch (token.Kind)
      {
        case OverlayFormulaTokenKind.Number:
          _builder.Append(token.Number.Replace('-', '−'));
          break;
        case OverlayFormulaTokenKind.Value:
          _builder
            .Append("<mark=#FFFFFF26> ")
            .Append(OverlayKeypad.ValueName(OverlayValueCatalog.Get(token.Value)).Replace("<", ""))
            .Append(" </mark>");
          break;
        default:
          _builder.Append("  ").Append(OverlayFormula.Symbol(token.Operator)).Append("  ");
          break;
      }
    }
    return _builder.ToString();
  }

  private readonly List<TextMeshProUGUI> _categories = new();
  private readonly List<(int Section, OverlayValueDefinition Value, Button Chip, float Width)> _placed = new();

  // The built-in categories, then a heading for each mod that adds values.
  private void BuildValues(
    UiFactory ui,
    TextMeshProUGUI measure,
    bool reduceMotion,
    Action<OverlayValueDefinition> pick
  )
  {
    foreach (OverlayValueSection section in OverlayValueCatalog.Sections())
    {
      if (section.Values.Count == 0)
        continue;
      TextMeshProUGUI label = ui.Text(
        ValueSectionNames.SectionName(section),
        Content,
        0,
        0,
        10,
        ChipHeight,
        13,
        UiPalette.Muted
      );
      label.textWrappingMode = TextWrappingModes.NoWrap;
      _categories.Add(label);
      int index = _categories.Count - 1;
      foreach (OverlayValueDefinition value in section.Values)
      {
        string text = OverlayKeypad.ValueName(value);
        float width = Mathf.Ceil(measure.GetPreferredValues(text).x) + 28;
        OverlayValueDefinition picked = value;
        Button chip = UiCapsules.CapsuleButton(
          ui,
          Content,
          "Value " + value.Key,
          text,
          0,
          0,
          width,
          ChipHeight,
          13,
          () => pick(picked),
          false,
          reduceMotion
        );
        chip.GetComponentInChildren<TextMeshProUGUI>().color = new Color(1, 1, 1, .92f);
        OverlayValueChipHandle handle = chip.gameObject.AddComponent<OverlayValueChipHandle>();
        handle.Value = value;
        handle.Dock = this;
        _chips.Add(chip);
        _placed.Add((index, value, chip, width));
      }
    }
  }

  // Category names line up in one column; each category's chips wrap only
  // within the space to their right.
  private void LayOutValues(float width)
  {
    float column = 0;
    foreach (TextMeshProUGUI label in _categories)
      column = Mathf.Max(column, Mathf.Ceil(label.GetPreferredValues(label.text).x));
    column += 24;
    float y = 0;
    for (int section = 0; section < _categories.Count; section++)
    {
      TextMeshProUGUI label = _categories[section];
      UiFactory.Place(label.rectTransform, 0, y, column, ChipHeight);
      float x = column;
      foreach ((int placedSection, _, Button chip, float chipWidth) in _placed)
      {
        if (placedSection != section)
          continue;
        if (x > column && x + chipWidth > width)
        {
          x = column;
          y += ChipHeight + RowGap;
        }
        UiFactory.Place((RectTransform)chip.transform, x, y, chipWidth, ChipHeight);
        x += chipWidth + ChipGap;
      }
      y += ChipHeight + RowGap + CategoryGap;
    }
    Content.sizeDelta = new Vector2(width, Mathf.Max(ChipHeight, y - RowGap - CategoryGap));
  }

  internal void BeginDrag(OverlayValueDefinition value, Vector2 screen)
  {
    Dragging = true;
    _begin(value, screen);
  }

  internal void Drag(Vector2 screen)
  {
    if (Dragging)
      _move(screen);
  }

  internal void EndDrag(Vector2 screen)
  {
    if (!Dragging)
      return;
    Dragging = false;
    _end(screen);
  }

  internal void CancelDrag() => Dragging = false;
}
