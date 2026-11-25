using Element.Domain.Overlay.Conditions;
using Element.Presentation.Ui.Theme;
using ElementUI;
using UnityEngine;

namespace Element.Presentation.OverlayEditor.Parts.IfTool;

/// <summary>
/// One case of an If as its card shows it: the value compared, how it
/// compares now, the number it is held against, which results turn the case
/// on, and its look in a line. The card of the look in the preview is open,
/// with its controls; the others read as one sentence. The last card is
/// "otherwise", which has a look and nothing to compare. The editor shows it
/// with <see cref="Show"/>; <see cref="IfCaseView"/> draws it.
/// </summary>
internal sealed class IfCaseViewModel : ViewModel
{
  private readonly UiColor _rest,
    _lit;

  /// <param name="index">The case's number; for otherwise, whatever keeps its card apart from the cases'.</param>
  /// <param name="under">What the card sits on; its fills are a little lighter than it.</param>
  public IfCaseViewModel(int index, bool otherwise, IIfToolActions editor, Color under)
  {
    Index = index;
    IsOtherwise = otherwise;
    Color rest = UiPalette.Wash(under, .07f);
    _rest = rest;
    _lit = Color.Lerp(rest, UiPalette.SoftAccent, .3f);
    Surface = rest;
    Raised = UiPalette.Wash(under, .13f);
    On = Color.Lerp(rest, UiPalette.SoftAccent, .34f);
    ValueFill = new State<UiColor>(Raised);
    NumberFill = new State<UiColor>(Raised);
    PickValue = Command(() => editor.PickValue(Index));
    EditNumber = Command(() => editor.EditNumber(Index));
    ToggleSmaller = Command(() => editor.Toggle(Index, OverlayComparison.Smaller));
    ToggleSame = Command(() => editor.Toggle(Index, OverlayComparison.Same));
    ToggleBigger = Command(() => editor.Toggle(Index, OverlayComparison.Bigger));
    Remove = Command(() => editor.RemoveCase(Index));
    ShowLook = Command(() => editor.ShowLook(Look.Value));
  }

  public readonly int Index;

  /// <summary>The card for when no case is on.</summary>
  public readonly bool IsOtherwise;

  /// <summary>The card's opaque fill.</summary>
  public readonly UiColor Surface;

  /// <summary>A control on the card, at rest.</summary>
  public readonly UiColor Raised;

  /// <summary>A result that turns the case on.</summary>
  public readonly UiColor On;

  /// <summary>The accent, for the value's name, a worked-out number and the edge of the case on now.</summary>
  public readonly UiColor Accent = UiPalette.SoftAccent;

  /// <summary>No edge.</summary>
  public readonly UiColor NoEdge = Color.clear;

  /// <summary>Its look's number in the If: its own index, or for otherwise the count of cases.</summary>
  public readonly State<int> Look = new(0);

  /// <summary>Its look is the one in the preview: the card shows its controls.</summary>
  public readonly State<bool> Open = new(false);

  /// <summary>It is the case on now, as the values are.</summary>
  public readonly State<bool> Now = new(false);

  /// <summary>The compared value's name, or what to do while there is none.</summary>
  public readonly State<string> ValueName = new("");

  /// <summary>The compared value's number now.</summary>
  public readonly State<string> ValueNow = new("");

  /// <summary>No value is picked yet: its chip asks for one.</summary>
  public readonly State<bool> ValueMissing = new(false);

  /// <summary>Lit while the values to compare are offered.</summary>
  public readonly State<UiColor> ValueFill;

  /// <summary>What the value is held against, as its result.</summary>
  public readonly State<string> Number = new("");

  /// <summary>The number is worked out from values.</summary>
  public readonly State<bool> NumberComputed = new(false);

  /// <summary>Lit while the dock types the number.</summary>
  public readonly State<UiColor> NumberFill;

  public readonly State<bool> Smaller = new(false);

  public readonly State<bool> Same = new(false);

  public readonly State<bool> Bigger = new(false);

  /// <summary>How the value compares now.</summary>
  public readonly State<OverlayComparison> Result = new(OverlayComparison.Unknown);

  /// <summary>The case in words, for the closed card: "X-Accuracy · 100 or bigger".</summary>
  public readonly State<string> Sentence = new("");

  /// <summary>The look as one line of text, or that it shows nothing.</summary>
  public readonly State<string> LookText = new("");

  public readonly State<UiColor> LookInk = new(Colors.Foreground);

  public Command PickValue { get; }

  public Command EditNumber { get; }

  public Command ToggleSmaller { get; }

  public Command ToggleSame { get; }

  public Command ToggleBigger { get; }

  public Command Remove { get; }

  public Command ShowLook { get; }

  /// <summary>Whether the case is on when its value compares as <paramref name="result"/>.</summary>
  [Pure]
  public bool TurnsOn(OverlayComparison result) =>
    result == OverlayComparison.Smaller ? Smaller.Value
    : result == OverlayComparison.Same ? Same.Value
    : Bigger.Value;

  /// <summary>Flips whether <paramref name="result"/> turns the case on.</summary>
  [Pure]
  public Command Toggle(OverlayComparison result) =>
    result == OverlayComparison.Smaller ? ToggleSmaller
    : result == OverlayComparison.Same ? ToggleSame
    : ToggleBigger;

  /// <summary>What every card shows: which look it is, whether it is open or on now, and the look in a line.</summary>
  public void ShowLookLine(int look, bool open, bool now, string text, Color ink, bool hidden)
  {
    Look.Value = look;
    Open.Value = open;
    Now.Value = now;
    LookText.Value = text;
    ink.a = 1;
    LookInk.Value = hidden ? Colors.Muted : ink;
  }

  /// <summary>What a case's card shows of its condition.</summary>
  public void Show(
    string valueName,
    string valueNow,
    bool valueMissing,
    bool valueOpen,
    string number,
    bool numberComputed,
    bool numberOpen,
    bool smaller,
    bool same,
    bool bigger,
    OverlayComparison result,
    string sentence
  )
  {
    ValueName.Value = valueName;
    ValueNow.Value = valueNow;
    ValueMissing.Value = valueMissing;
    ValueFill.Value = valueOpen ? _lit : Raised;
    Number.Value = number;
    NumberComputed.Value = numberComputed;
    NumberFill.Value = numberOpen ? _lit : Raised;
    Smaller.Value = smaller;
    Same.Value = same;
    Bigger.Value = bigger;
    Result.Value = result;
    Sentence.Value = sentence;
  }
}
