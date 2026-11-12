using System;
using System.Collections.Generic;
using System.Text;
using Element.Domain.Overlay.Formula;
using Element.Domain.Overlay.Values;
using Element.Presentation.Ui.Theme;
using ElementUI;
using UnityEngine;
using static Element.Presentation.Ui.Theme.UiLocale;

namespace Element.Presentation.Layout.ContentEditors.Shell;

/// <summary>
/// What the input dock shows and does: its line (what is edited and its
/// formula, or with nothing edited what to do), the result, every value by
/// category and the keypad. A value can be clicked or carried out of the
/// dock. <see cref="OverlayInputDockView"/> draws it; the editor refreshes it
/// from what is being typed.
/// </summary>
internal sealed class OverlayInputDockModel : ViewModel
{
  private static readonly Color Resting = new(1, 1, 1, .5f),
    Placeholder = new(1, 1, 1, .32f),
    Line = new(1, 1, 1, .16f);

  private readonly Action<OverlayValueDefinition, Vector2> _begin;
  private readonly Action<Vector2> _move;
  private readonly Action<Vector2> _end;
  private readonly StringBuilder _builder = new(128);
  private readonly OverlayFormula _described = new();
  private string _description;
  private string _name,
    _nameLine,
    _result,
    _resultLine;
  private bool _typing;
  private int _drop;

  /// <param name="press">A key typed: a digit, the point, an operator or '\b'.</param>
  /// <param name="clear">AC.</param>
  /// <param name="done">Done.</param>
  /// <param name="pick">A value clicked.</param>
  /// <param name="begin">A value starts being carried out of the dock, at a screen point.</param>
  /// <param name="move">The carried value moved.</param>
  /// <param name="end">The carried value was let go.</param>
  public OverlayInputDockModel(
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
    Keypad = Own(new OverlayKeypadModel(press, clear, done));
    Sections = BuildSections(pick);
  }

  public readonly OverlayKeypadModel Keypad;

  /// <summary>The built-in categories, then a heading for each mod that adds values; none empty.</summary>
  public readonly IReadOnlyList<ValueSection> Sections;

  /// <summary>What is edited, as "Width  =", or with nothing edited what to do.</summary>
  public readonly State<string> Title = new("");

  public readonly State<Color> TitleInk = new(Resting);

  /// <summary>The formula as it reads, values as soft chips (rich text); a hint while it is empty.</summary>
  public readonly State<string> Formula = new("");

  public readonly State<Color> FormulaInk = new(UiPalette.Foreground);

  /// <summary>Nothing typed yet: the caret sits at the start, before the hint.</summary>
  public readonly State<bool> FormulaEmpty = new(false);

  /// <summary>The result on the right, as "= 160 px"; empty for none.</summary>
  public readonly State<string> Result = new("");

  /// <summary>Something is being typed: the caret shows.</summary>
  public readonly State<bool> Typing = new(false);

  /// <summary>The caret's blink: on or off now.</summary>
  public readonly State<bool> Caret = new(false);

  /// <summary>The line under the formula: lit while a carried value can (or can't) join it.</summary>
  public readonly State<Color> Underline = new(Line);

  /// <summary>What the dock edits, such as "width" or "value:3"; null while nothing is.</summary>
  public string Owner { get; private set; }

  /// <summary>A value is being carried out of the dock.</summary>
  public bool Dragging { get; private set; }

  /// <summary>What the dock edits from now on; null to rest.</summary>
  public void Edit(string owner) => Owner = owner;

  /// <summary>
  /// Shows <paramref name="name"/> and the formula <paramref name="editing"/>
  /// types, with <paramref name="result"/> (already in its unit) on the right;
  /// with nothing being edited, <paramref name="hint"/> says what to do.
  /// </summary>
  public void Refresh(OverlayFormulaEditing editing, string name, string result, string hint)
  {
    _typing = Owner != null && editing.Active;
    if (_typing && name != _name)
    {
      _name = name;
      _nameLine = name + "  =";
    }
    Title.Value = _typing ? _nameLine : hint;
    TitleInk.Value = _typing ? UiPalette.Muted : Resting;
    if (!_typing || string.IsNullOrEmpty(result))
      Result.Value = "";
    else
    {
      if (result != _result)
      {
        _result = result;
        _resultLine = "= " + result;
      }
      Result.Value = _resultLine;
    }
    bool empty = _typing && editing.Working.IsEmpty;
    FormulaEmpty.Value = empty;
    FormulaInk.Value = empty ? Placeholder : UiPalette.Foreground;
    Formula.Value =
      !_typing ? ""
      : empty ? L("숫자를 누르거나 값을 고르세요", "Type a number or pick a value")
      : Describe(editing.Working);
    Typing.Value = _typing;
    // A redraw starts the blink over: the caret shows at once.
    Caret.Value = _typing;
    Keypad.Refresh(editing);
  }

  /// <summary>Once a frame: the caret blinks while something is typed.</summary>
  public void Blink(bool visible) => Caret.Value = _typing && visible;

  /// <summary>0 nothing, 1 a carried value can join the formula, -1 it can't.</summary>
  public void SetDrop(int state)
  {
    if (_drop == state)
      return;
    _drop = state;
    Underline.Value =
      state > 0 ? UiPalette.SoftAccent
      : state < 0 ? UiPalette.Error
      : Line;
  }

  // The formula as it reads: values as soft chips, operators spaced out. Made
  // again only when the formula changed: the editor refreshes every frame a
  // level plays behind it.
  private string Describe(OverlayFormula formula)
  {
    if (_description != null && formula.SameAs(_described))
      return _description;
    _described.Clear();
    for (int i = 0; i < formula.Count; i++)
      _described.Add(formula[i]);
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
            .Append(ValueSectionNames.ValueName(OverlayValueCatalog.Get(token.Value)).Replace("<", ""))
            .Append(" </mark>");
          break;
        default:
          _builder.Append("  ").Append(OverlayFormula.Symbol(token.Operator)).Append("  ");
          break;
      }
    }
    return _description = _builder.ToString();
  }

  // ---- Carrying a value out ----

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

  private IReadOnlyList<ValueSection> BuildSections(Action<OverlayValueDefinition> pick)
  {
    List<ValueSection> sections = new();
    foreach (OverlayValueSection section in OverlayValueCatalog.Sections())
    {
      if (section.Values.Count == 0)
        continue;
      ValueChipModel[] chips = new ValueChipModel[section.Values.Count];
      for (int i = 0; i < chips.Length; i++)
        chips[i] = Own(new ValueChipModel(section.Values[i], pick));
      sections.Add(new ValueSection(ValueSectionNames.SectionName(section), chips));
    }
    return sections;
  }
}

/// <summary>A category of values in the dock: its heading and its values.</summary>
internal sealed class ValueSection
{
  public ValueSection(string name, IReadOnlyList<ValueChipModel> chips)
  {
    Name = name;
    Chips = chips;
  }

  public readonly string Name;
  public readonly IReadOnlyList<ValueChipModel> Chips;
}

/// <summary>One value in the dock: a chip that a click picks and a drag carries out.</summary>
internal sealed class ValueChipModel : ViewModel
{
  public ValueChipModel(OverlayValueDefinition value, Action<OverlayValueDefinition> pick)
  {
    Value = value;
    Name = "Value " + value.Key;
    Label = ValueSectionNames.ValueName(value);
    Pick = Command(() => pick(value));
  }

  public readonly OverlayValueDefinition Value;

  /// <summary>Names the drawn chip, for the checks: "Value combo".</summary>
  public readonly string Name;

  public readonly string Label;

  public Command Pick { get; }
}
