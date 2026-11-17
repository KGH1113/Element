using System.Text;
using Element.Domain.Overlay.Formula;
using Element.Domain.Overlay.Values;
using Element.Presentation.Layout.ContentEditors.Shell.Dock.Keypad;
using Element.Presentation.Layout.ContentEditors.Shell.Dock.ValueBrowser;
using Element.Presentation.Overlay.Values;
using Element.Presentation.Ui.Theme;
using ElementUI;
using UnityEngine;
using static Element.Presentation.Ui.Theme.UiLocale;

namespace Element.Presentation.Layout.ContentEditors.Shell.Dock;

/// <summary>
/// What the input dock shows: its line (what is edited and its formula, or
/// with nothing edited what to do), the result, the values and the keypad.
/// </summary>
/// <remarks>
/// Data goes one way. The editor says what is edited (<see cref="Show"/>,
/// <see cref="Blink"/>, <see cref="SetDrop"/>); this turns it into what the
/// line reads; <see cref="InputDockView"/> draws that. What the player does
/// in the dock goes back up through <see cref="IInputDockActions"/>, which the
/// keypad and the values were given: the dock itself changes nothing that is
/// edited.
/// </remarks>
internal sealed class InputDockViewModel : ViewModel
{
  private static readonly Color Resting = new(1, 1, 1, .5f),
    Placeholder = new(1, 1, 1, .32f),
    Line = new(1, 1, 1, .16f);

  private readonly StringBuilder _builder = new(128);
  private readonly OverlayFormula _described = new();
  private string _description;
  private string _name,
    _nameLine,
    _result,
    _resultLine;
  private bool _typing;
  private ValueDrop _drop;

  /// <param name="values">What the overlay shows now, for the chips; null shows names only.</param>
  /// <param name="notices">The editor's notices, which the dock shows above its line.</param>
  /// <param name="actions">Where everything the player does in the dock goes.</param>
  public InputDockViewModel(OverlayValues values, OverlayEditorFrameModel notices, IInputDockActions actions)
  {
    Notices = notices;
    Keypad = Own(new KeypadViewModel(actions));
    Values = Own(new ValueBrowserViewModel(values, actions));
  }

  // ---- Its parts ----

  public readonly KeypadViewModel Keypad;

  /// <summary>Every value that can be added: the places, the open one's chips and the search.</summary>
  public readonly ValueBrowserViewModel Values;

  /// <summary>The editor's notices, shown above the line for a few seconds.</summary>
  public readonly OverlayEditorFrameModel Notices;

  // ---- What the view reads ----

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

  /// <summary>The caret at the formula's end, while something is typed: on or off as it blinks.</summary>
  public readonly State<bool> Caret = new(false);

  /// <summary>The line under the formula: lit while a carried value can (or can't) join it.</summary>
  public readonly State<Color> Underline = new(Line);

  /// <summary>What the dock edits, such as "width" or "value:3"; null while nothing is.</summary>
  public string Owner { get; private set; }

  // ---- What the editor tells it ----

  /// <summary>
  /// What the dock edits now: <paramref name="owner"/> (null for nothing),
  /// named <paramref name="name"/>, with the formula <paramref name="editing"/>
  /// types and <paramref name="result"/> (already in its unit) on the right.
  /// With nothing being edited, <paramref name="hint"/> says what to do.
  /// </summary>
  public void Show(string owner, OverlayFormulaEditing editing, string name, string result, string hint)
  {
    Owner = owner;
    _typing = owner != null && editing.Active;
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
    // A redraw starts the blink over: the caret shows at once.
    Caret.Value = _typing;
    Keypad.Show(editing);
  }

  /// <summary>Once a frame: the caret blinks while something is typed.</summary>
  public void Blink(bool visible) => Caret.Value = _typing && visible;

  /// <summary>Whether a carried value is over the formula line, and whether it can join the formula.</summary>
  public void SetDrop(ValueDrop drop)
  {
    if (_drop == drop)
      return;
    _drop = drop;
    Underline.Value =
      drop == ValueDrop.Accepts ? UiPalette.SoftAccent
      : drop == ValueDrop.Refuses ? UiPalette.Error
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
}
