using System.Text;
using Element.Domain.Overlay.Formula;
using Element.Domain.Overlay.Values;
using Element.Presentation.Overlay.Values;
using Element.Presentation.OverlayEditor.Parts.Dock.Keypad;
using Element.Presentation.OverlayEditor.Parts.Dock.ValueBrowser;
using Element.Presentation.OverlayEditor.Parts.EditorFrame;
using Element.Presentation.OverlayEditor.Parts.Formula;
using Element.Presentation.Ui.Theme;
using ElementUI;
using UnityEngine;
using static Element.Presentation.Ui.Theme.UiLocale;

namespace Element.Presentation.OverlayEditor.Parts.Dock;

/// <summary>
/// What the input dock shows: its line (what is edited and its formula, or
/// with nothing edited what to do), the result, the values and the keypad.
/// </summary>
/// <remarks>
/// Data goes one way. The formula being typed (<see cref="FormulaEditing"/>)
/// tells its readers when it changes, so the line and the keypad follow each
/// key by themselves; the editor only says what is edited and what to call it
/// (<see cref="Show"/>). <see cref="InputDockView"/> draws it. What the player
/// does in the dock goes back up through <see cref="IInputDockActions"/>,
/// which the keypad and the values were given: the dock itself changes
/// nothing that is edited.
/// </remarks>
internal sealed class InputDockViewModel : ViewModel
{
  // The caret's on and off halves, as a text field's.
  private const float BlinkSeconds = .53f;

  private static readonly Color Resting = new(1, 1, 1, .5f),
    Placeholder = new(1, 1, 1, .32f),
    Line = new(1, 1, 1, .16f),
    Muted = UiPalette.Muted,
    Ink = UiPalette.Foreground;

  // The line is described on the main thread only, one at a time.
  private static readonly StringBuilder Builder = new(128);

  private readonly FormulaEditing _editing;
  private readonly Timeline _blink = Timeline.Periodic(BlinkSeconds);
  private readonly bool _steadyCaret;
  private readonly string _emptyHint;
  private readonly State<string> _name = new(""),
    _result = new(""),
    _hint = new("");
  private ValueDrop _drop;

  /// <param name="values">What the overlay shows now, for the chips; null shows names only.</param>
  /// <param name="editing">The formula the keypad types; the line shows it as it changes.</param>
  /// <param name="notices">The editor's notices, which the dock shows above its line.</param>
  /// <param name="actions">Where everything the player does in the dock goes.</param>
  /// <param name="reduceMotion">The caret stays on instead of blinking.</param>
  public InputDockViewModel(
    OverlayValues values,
    FormulaEditing editing,
    EditorFrameViewModel notices,
    IInputDockActions actions,
    bool reduceMotion
  )
  {
    _editing = editing;
    _steadyCaret = reduceMotion;
    _emptyHint = L("숫자를 누르거나 값을 고르세요", "Type a number or pick a value");
    Notices = notices;
    Keypad = Own(new KeypadViewModel(actions, editing));
    Values = Own(new ValueBrowserViewModel(values, actions));
    Typing = Computed(() => Owner.Value != null && _editing.Active);
    Title = Computed(() => Typing.Value ? _name.Value + "  =" : _hint.Value);
    TitleInk = Computed(() => Typing.Value ? Muted : Resting);
    FormulaEmpty = Computed(() => Typing.Value && IsEmpty(_editing));
    FormulaInk = Computed(() => FormulaEmpty.Value ? Placeholder : Ink);
    Formula = Computed(() =>
      !Typing.Value ? ""
      : FormulaEmpty.Value ? _emptyHint
      : Describe(_editing)
    );
    Result = Computed(() => Typing.Value && _result.Value.Length > 0 ? "= " + _result.Value : "");
    // While nothing is typed the caret doesn't read the blink, which then rests.
    Caret = Computed(() => Typing.Value && (_steadyCaret || _blink.Even));
  }

  // ---- Its parts ----

  public readonly KeypadViewModel Keypad;

  /// <summary>Every value that can be added: the places, the open one's chips and the search.</summary>
  public readonly ValueBrowserViewModel Values;

  /// <summary>The editor's notices, shown above the line for a few seconds.</summary>
  public readonly EditorFrameViewModel Notices;

  // ---- What the view reads ----

  /// <summary>Something is open in the dock and the keypad has it.</summary>
  public readonly Computed<bool> Typing;

  /// <summary>What is edited, as "Width  =", or with nothing edited what to do.</summary>
  public readonly Computed<string> Title;

  public readonly Computed<Color> TitleInk;

  /// <summary>The formula as it reads, values as soft chips (rich text); a hint while it is empty.</summary>
  public readonly Computed<string> Formula;

  public readonly Computed<Color> FormulaInk;

  /// <summary>Nothing typed yet: the caret sits at the start, before the hint.</summary>
  public readonly Computed<bool> FormulaEmpty;

  /// <summary>The result on the right, as "= 160 px"; empty for none.</summary>
  public readonly Computed<string> Result;

  /// <summary>The caret at the formula's end, while something is typed: on or off as it blinks.</summary>
  public readonly Computed<bool> Caret;

  /// <summary>The line under the formula: lit while a carried value can (or can't) join it.</summary>
  public readonly State<Color> Underline = new(Line);

  /// <summary>What the dock edits, such as "width" or "value:3"; null while nothing is.</summary>
  public readonly State<string> Owner = new(null);

  // ---- What the editor tells it ----

  /// <summary>
  /// What the dock edits now: <paramref name="owner"/> (null for nothing),
  /// named <paramref name="name"/>, with <paramref name="result"/> (already
  /// in its unit) on the right. With nothing being edited,
  /// <paramref name="hint"/> says what to do. The formula itself the dock
  /// reads from what the keypad types.
  /// </summary>
  public void Show(string owner, string name, string result, string hint)
  {
    Owner.Value = owner;
    _name.Value = name ?? "";
    _result.Value = result ?? "";
    _hint.Value = hint ?? "";
  }

  /// <summary>The editor drew afresh after the player did something: the caret shows at once and blinks from there.</summary>
  public void RestartBlink() => _blink.Restart();

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

  protected override void OnDisposed() => _blink.Dispose();

  [Pure]
  private static bool IsEmpty(FormulaEditing editing) => editing.Working == null || editing.Working.IsEmpty;

  // The formula as it reads: values as soft chips, operators spaced out. Made
  // again only when the formula changes, not when the editor draws again.
  [Pure]
  private static string Describe(FormulaEditing editing)
  {
    OverlayFormula formula = editing.Working;
    if (formula == null)
      return "";
    StringBuilder builder = Builder.Clear();
    for (int i = 0; i < formula.Count; i++)
    {
      OverlayFormulaToken token = formula[i];
      switch (token.Kind)
      {
        case OverlayFormulaTokenKind.Number:
          builder.Append(token.Number.Replace('-', '−'));
          break;
        case OverlayFormulaTokenKind.Value:
          builder
            .Append("<mark=#FFFFFF26> ")
            .Append(ValueSectionNames.ValueName(OverlayValueCatalog.Get(token.Value)).Replace("<", ""))
            .Append(" </mark>");
          break;
        default:
          builder.Append("  ").Append(OverlayFormula.Symbol(token.Operator)).Append("  ");
          break;
      }
    }
    return builder.ToString();
  }
}
