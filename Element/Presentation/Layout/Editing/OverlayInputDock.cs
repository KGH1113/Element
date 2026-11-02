using System;
using System.Collections.Generic;
using System.Text;
using Element.Domain.Elements;
using Element.Presentation.Settings;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using static Element.Presentation.Settings.SettingsLocale;

namespace Element.Presentation.Layout;

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
    SettingsUiFactory ui,
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
    _name = ui.Text("", _line, 0, 0, 10, LineHeight, 17, SettingsUiFactory.Muted);
    _name.textWrappingMode = TextWrappingModes.NoWrap;
    _name.overflowMode = TextOverflowModes.Ellipsis;
    _formulaViewport = ui.Rect("Formula", _line, 0, 0, 10, LineHeight);
    _formulaViewport.gameObject.AddComponent<RectMask2D>();
    _formula = ui.Text("", _formulaViewport, 0, 0, 10, LineHeight, 26);
    _formula.richText = true;
    _formula.textWrappingMode = TextWrappingModes.NoWrap;
    _formula.overflowMode = TextOverflowModes.Overflow;
    _caret = ui.Panel("Caret", _formulaViewport, 0, 14, 2, LineHeight - 28, Color.white, false);
    _result = ui.Text("", _line, 0, 0, 10, LineHeight, 22, SettingsPalette.SoftAccent);
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
    SettingsUiFactory.Place(_values, 0, LineHeight + LineGap, valuesWidth, OverlayKeypad.Height);
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
    _name.color = _editing ? SettingsUiFactory.Muted : new Color(1, 1, 1, .5f);
    float nameWidth = Mathf.Min(_width, Mathf.Ceil(_name.GetPreferredValues(_name.text).x) + 4);
    SettingsUiFactory.Place(_name.rectTransform, x, 0, nameWidth, LineHeight);
    x += nameWidth + 12;
    _result.text = _editing && !string.IsNullOrEmpty(result) ? "= " + result : "";
    _result.gameObject.SetActive(_result.text.Length > 0);
    float resultWidth = _result.text.Length > 0 ? Mathf.Ceil(_result.GetPreferredValues(_result.text).x) + 4 : 0;
    SettingsUiFactory.Place(_result.rectTransform, _width - resultWidth, 0, resultWidth, LineHeight);
    float available = Mathf.Max(40, _width - resultWidth - 24 - x);
    SettingsUiFactory.Place(_formulaViewport, x, 0, available, LineHeight);
    _formula.text = _editing ? Describe(editing.Working) : "";
    if (_editing && editing.Working.IsEmpty)
    {
      _formula.text = L("숫자를 누르거나 값을 고르세요", "Type a number or pick a value");
      _formula.color = new Color(1, 1, 1, .32f);
    }
    else
      _formula.color = SettingsUiFactory.Foreground;
    // A long formula keeps its end in view, where the caret is.
    float textWidth = Mathf.Ceil(_formula.GetPreferredValues(_formula.text).x) + 2;
    float left = editing.Working != null && !editing.Working.IsEmpty ? Mathf.Min(0, available - textWidth - 6) : 0;
    SettingsUiFactory.Place(_formula.rectTransform, left, 0, Mathf.Max(textWidth, 10), LineHeight);
    float caretX = editing.Working != null && editing.Working.IsEmpty ? 0 : left + textWidth + 2;
    SettingsUiFactory.Place(_caret.rectTransform, caretX, 15, 2, LineHeight - 30);
    _caret.enabled = _editing;
    Keypad.Refresh(editing);
  }

  public void Blink(bool visible) => _caret.enabled = _editing && visible;

  public bool Contains(Vector2 screen) => OverlayEditorUi.Contains(Root, screen);

  /// <summary>The formula line, where a dropped value joins the formula.</summary>
  public bool FormulaContains(Vector2 screen) => Owner != null && OverlayEditorUi.Contains(_line, screen);

  /// <summary>0 nothing, 1 a dragged value can join here, -1 it can't.</summary>
  public void SetDrop(int state)
  {
    if (_drop == state)
      return;
    _drop = state;
    Color accent = SettingsPalette.SoftAccent;
    _underline.color =
      state > 0 ? accent
      : state < 0 ? OverlayEditorUi.Warning
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

  private readonly List<(OverlayValueCategory Category, TextMeshProUGUI Label)> _categories = new();
  private readonly List<(OverlayValueDefinition Value, Button Chip, float Width)> _placed = new();

  private void BuildValues(
    SettingsUiFactory ui,
    TextMeshProUGUI measure,
    bool reduceMotion,
    Action<OverlayValueDefinition> pick
  )
  {
    foreach (OverlayValueCategory category in OverlayValueCatalog.Categories)
    {
      TextMeshProUGUI label = ui.Text(
        OverlayEditorUi.CategoryName(category),
        Content,
        0,
        0,
        10,
        ChipHeight,
        13,
        SettingsUiFactory.Muted
      );
      label.textWrappingMode = TextWrappingModes.NoWrap;
      _categories.Add((category, label));
      foreach (OverlayValueDefinition value in OverlayValueCatalog.All)
      {
        if (value.Category != category)
          continue;
        string text = OverlayKeypad.ValueName(value);
        float width = Mathf.Ceil(measure.GetPreferredValues(text).x) + 28;
        OverlayValueDefinition picked = value;
        Button chip = OverlayEditorUi.CapsuleButton(
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
        _placed.Add((value, chip, width));
      }
    }
  }

  // Category names line up in one column; each category's chips wrap only
  // within the space to their right.
  private void LayOutValues(float width)
  {
    float column = 0;
    foreach ((_, TextMeshProUGUI label) in _categories)
      column = Mathf.Max(column, Mathf.Ceil(label.GetPreferredValues(label.text).x));
    column += 24;
    float y = 0;
    foreach ((OverlayValueCategory category, TextMeshProUGUI label) in _categories)
    {
      SettingsUiFactory.Place(label.rectTransform, 0, y, column, ChipHeight);
      float x = column;
      foreach ((OverlayValueDefinition value, Button chip, float chipWidth) in _placed)
      {
        if (value.Category != category)
          continue;
        if (x > column && x + chipWidth > width)
        {
          x = column;
          y += ChipHeight + RowGap;
        }
        SettingsUiFactory.Place((RectTransform)chip.transform, x, y, chipWidth, ChipHeight);
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

// Hands a chip's drag to its dock. Taking the drag also keeps the values from
// scrolling while one is carried out.
internal sealed class OverlayValueChipHandle
  : MonoBehaviour,
    IInitializePotentialDragHandler,
    IBeginDragHandler,
    IDragHandler,
    IEndDragHandler
{
  public OverlayValueDefinition Value;
  public OverlayInputDock Dock;

  public void OnInitializePotentialDrag(PointerEventData data) => data.useDragThreshold = true;

  public void OnBeginDrag(PointerEventData data)
  {
    if (data.button == PointerEventData.InputButton.Left)
      Dock?.BeginDrag(Value, data.position);
  }

  public void OnDrag(PointerEventData data) => Dock?.Drag(data.position);

  public void OnEndDrag(PointerEventData data) => Dock?.EndDrag(data.position);

  // A dock closed mid-drag drops nothing.
  private void OnDisable() => Dock?.CancelDrag();
}
