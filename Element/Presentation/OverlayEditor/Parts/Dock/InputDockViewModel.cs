using System.Collections.Generic;
using System.Text;
using Element.Domain.Overlay.Values;
using Element.Domain.Overlay.Variables;
using Element.Presentation.Overlay.Text;
using Element.Presentation.Overlay.Values;
using Element.Presentation.OverlayEditor.Parts.Dock.Keypad;
using Element.Presentation.OverlayEditor.Parts.Dock.Line;
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
  private readonly FormulaEditing _editing;
  private readonly IInputDockActions _actions;
  private readonly string _emptyHint;

  /// <param name="values">What the overlay shows now, for the chips; null shows names only.</param>
  /// <param name="editing">The formula the keypad types; the line shows it as it changes.</param>
  /// <param name="notices">The editor's notices, which the dock shows above its line.</param>
  /// <param name="actions">Where everything the player does in the dock goes.</param>
  /// <param name="reduceMotion">Unused since the line has no caret; kept for the editors that pass it.</param>
  public InputDockViewModel(
    OverlayValues values,
    FormulaEditing editing,
    EditorFrameViewModel notices,
    IInputDockActions actions,
    bool reduceMotion
  )
  {
    _editing = editing;
    _actions = actions;
    _emptyHint = L("칸이나 값을 누르면 여기서 고쳐요", "Click a field or a value to edit it here");
    Notices = notices;
    Keypad = Own(new KeypadViewModel(actions, editing));
    Values = Own(new ValueBrowserViewModel(values, actions));
    Line = Own(new LineViewModel(actions));
    Typing = Computed(() => Owner.Value != null && _editing.Active);
    Problem = Computed(() => Typing.Value ? FormulaRefusals.Explain(_editing.Problem) ?? "" : "");
    Method = Computed(() => MethodOf(_editing));
    SwitchOn = Computed(() => PickedBool(_editing));
    CompareOn = Computed(() => PickedCompare(_editing));
    Color rest = UiPalette.EditorControl;
    Rest = rest;
    Lit = Color.Lerp(rest, UiPalette.SoftAccent, .34f);
    Warn = Color.Lerp(rest, UiPalette.Error, .22f);
    AccentInk = UiPalette.SoftAccent;
    OnInk = UiPalette.Foreground;
    Values.Fits = value => !_editing.Active || _editing.CanDrop(value);
  }

  // ---- The input method ----

  /// <summary>What the picked piece is edited with.</summary>
  public readonly Computed<DockMethod> Method;

  /// <summary>Why the field can't be finished as it is; empty while it can.</summary>
  public readonly Computed<string> Problem;

  /// <summary>The picked true or false; null when none is picked.</summary>
  public readonly Computed<bool?> SwitchOn;

  /// <summary>The operator picked in a condition's operator slot; '\0' for none.</summary>
  public readonly Computed<char> CompareOn;

  public readonly UiColor Rest,
    Lit,
    Warn,
    AccentInk,
    OnInk;

  /// <summary>The picked words, which the keyboard input method types.</summary>
  public string Words
  {
    [Pure]
    get => _editing.SelectedPiece is { Kind: OverlayPieceKind.Text } piece ? piece.Text : "";
    set => _actions.Words(value);
  }

  /// <summary>The picked made value's name, which the keyboard renames.</summary>
  public string CustomName
  {
    [Pure]
    get =>
      _editing.SelectedPiece is { Kind: OverlayPieceKind.Custom } piece
        ? OverlayCustomValues.NameOf(piece.Text) ?? ""
        : "";
    set => _actions.Rename(value);
  }

  public void SetSwitch(bool value) => _actions.Switch(value);

  public void PressCompare(char op) => _actions.Press(op);

  // The operator in a condition's operator slot; '\0' for none.
  [Pure]
  private static char PickedCompare(FormulaEditing editing) =>
    editing.Active && editing.Kind == OverlayValueType.Compare && editing.Working.Count == 1
      ? editing.Working[0].Op
      : '\0';

  [Pure]
  private static bool? PickedBool(FormulaEditing editing) =>
    editing.SelectedPiece is { Kind: OverlayPieceKind.Bool } piece ? piece.Bool : null;

  // ---- Its parts ----

  public readonly KeypadViewModel Keypad;

  /// <summary>Every value that can be added: the places, the open one's chips and the search.</summary>
  public readonly ValueBrowserViewModel Values;

  /// <summary>The editor's notices, shown above the line for a few seconds.</summary>
  public readonly EditorFrameViewModel Notices;

  /// <summary>The line: what is open and its pieces laid out, any of them a click away.</summary>
  public readonly LineViewModel Line;

  // ---- What the view reads ----

  /// <summary>Something is open in the dock and the keypad has it.</summary>
  public readonly Computed<bool> Typing;

  /// <summary>What the dock edits, such as "width" or "value:3"; null while nothing is.</summary>
  public readonly State<string> Owner = new(null);

  // ---- What the editor tells it ----

  /// <summary>
  /// What the dock edits now: <paramref name="owner"/> (null for nothing),
  /// named <paramref name="name"/>, with <paramref name="result"/> (already
  /// in its unit) on the right. With nothing being edited,
  /// <paramref name="hint"/> says what to do. The formula itself the dock
  /// reads from what the keypad types; <paramref name="otherwise"/> is what
  /// shows when none of its cases holds (rich text), said after them.
  /// </summary>
  public void Show(string owner, string name, string result, string hint, string otherwise = null)
  {
    Owner.Value = owner;
    Line.Show(owner != null ? _editing : null, name, result, string.IsNullOrEmpty(hint) ? _emptyHint : hint);
    Runtime.Probe();
  }

  // What the picked piece is edited with; the operator slot has its comparisons.
  [Pure]
  private static DockMethod MethodOf(FormulaEditing editing)
  {
    if (!editing.Active)
      return DockMethod.Keypad;
    if (editing.Kind == OverlayValueType.Compare)
      return DockMethod.Compare;
    OverlayPiece piece = editing.SelectedPiece;
    if (piece == null)
      return editing.Kind == OverlayValueType.Color ? DockMethod.Color
        : editing.Kind == OverlayValueType.Bool ? DockMethod.Switch
        : DockMethod.Keypad;
    return piece.Kind switch
    {
      OverlayPieceKind.Text => DockMethod.Words,
      OverlayPieceKind.Bool => DockMethod.Switch,
      OverlayPieceKind.Condition => DockMethod.Condition,
      OverlayPieceKind.Group => DockMethod.Group,
      OverlayPieceKind.Custom => DockMethod.Custom,
      OverlayPieceKind.Color => DockMethod.Color,
      OverlayPieceKind.Value when OverlayValueCatalog.Get(piece.Value).IsColor => DockMethod.Color,
      _ => DockMethod.Keypad,
    };
  }

  /// <summary>A round swatch of <paramref name="color"/> leading a color in the line.</summary>
  public static StringBuilder Swatch(StringBuilder builder, Color color)
  {
    color.a = 1;
    return builder.Append("<color=#").AppendHexRgb(color).Append(">●</color> ");
  }
}
