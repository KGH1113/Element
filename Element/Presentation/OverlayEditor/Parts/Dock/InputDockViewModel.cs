using Element.Configuration.Overlay;
using Element.Domain.Layout;
using Element.Domain.Overlay.Values;
using Element.Domain.Overlay.Variables;
using Element.Presentation.Overlay.Values;
using Element.Presentation.OverlayEditor.Parts.Dock.Condition;
using Element.Presentation.OverlayEditor.Parts.Dock.Keypad;
using Element.Presentation.OverlayEditor.Parts.Dock.Line;
using Element.Presentation.OverlayEditor.Parts.Dock.ValueBrowser;
using Element.Presentation.OverlayEditor.Parts.Formula;
using Element.Presentation.OverlayEditor.Parts.Grow;
using Element.Presentation.Ui.Theme;
using ElementUI;
using UnityEngine;

namespace Element.Presentation.OverlayEditor.Parts.Dock;

/// <summary>
/// What the input dock shows: on the left its line (the field open and what
/// it holds) over the values that fit there, on the right the input method
/// of what is picked. It says nothing in words: what can't be finished or
/// can't take what was given shakes (<see cref="Alarm"/>).
/// </summary>
/// <remarks>
/// Data goes one way. The formula being typed (<see cref="FormulaEditing"/>)
/// tells its readers when it changes, so the line and the keypad follow each
/// key by themselves; the editor only says what is edited and what to call it
/// (<see cref="Show"/>). What the player does in the dock goes back up
/// through <see cref="IInputDockActions"/>: the dock itself changes nothing
/// that is edited.
/// </remarks>
internal sealed class InputDockViewModel : ViewModel
{
  private readonly FormulaEditing _editing;
  private readonly IInputDockActions _actions;
  private readonly bool _reduceMotion;
  private readonly IOverlayNumberSource _samples;

  /// <param name="values">What the overlay shows now, for the chips; null shows names only.</param>
  /// <param name="editing">The formula the keypad types; the line shows it as it changes.</param>
  /// <param name="actions">Where everything the player does in the dock goes.</param>
  /// <param name="reduceMotion">Nothing shakes; what is wrong only changes color.</param>
  /// <param name="caret">With nothing open, values go in at a text's caret, and the keyboard types there.</param>
  public InputDockViewModel(
    OverlayValues values,
    FormulaEditing editing,
    IInputDockActions actions,
    bool reduceMotion,
    bool caret = false
  )
  {
    _editing = editing;
    _actions = actions;
    _reduceMotion = reduceMotion;
    // What the game reads now, or each value's example while it reads nothing: which way a condition goes.
    _samples = values?.WithSamples;
    Keyboard = caret;
    // A text is its own content field, open all along: there is always something to apply.
    Keypad = Own(new KeypadViewModel(actions, editing, Choice, caret));
    Values = Own(new ValueBrowserViewModel(values, actions, editing, caret));
    Line = Own(new LineViewModel(actions));
    Form = Own(new ConditionFormViewModel(actions.Enter));
    Points = Own(new GrowPickerViewModel(actions.Point));
    Typing = Computed(() => Owner.Value != null && _editing.Active);
    Method = Computed(() => MethodOf(_editing, Choice.Value));
    SwitchOn = Computed(() => PickedBool(_editing));
    CompareOn = Computed(() => PickedCompare(_editing));
    Color rest = UiPalette.EditorControl;
    Rest = rest;
    Lit = Color.Lerp(rest, UiPalette.SoftAccent, .34f);
    AccentInk = UiPalette.SoftAccent;
    OnInk = UiPalette.Foreground;
  }

  // ---- The input method ----

  /// <summary>What the picked piece is edited with.</summary>
  public readonly Computed<DockMethod> Method;

  /// <summary>
  /// The field open that is picked from a list of its own (a font) and so
  /// has no values on the line: its input method; <see cref="DockMethod.Rest"/> for none.
  /// </summary>
  public readonly State<DockMethod> Choice = new(DockMethod.Rest);

  /// <summary>At rest the keyboard types into the text: the place shows a keyboard.</summary>
  public readonly bool Keyboard;

  /// <summary>The picked true or false; null when none is picked.</summary>
  public readonly Computed<bool?> SwitchOn;

  /// <summary>The picked comparison; '\0' for none.</summary>
  public readonly Computed<char> CompareOn;

  public readonly UiColor Rest,
    Lit,
    AccentInk,
    OnInk;

  /// <summary>No outline: what a choice that doesn't hold wears.</summary>
  public readonly UiColor NoEdge = new Color(0, 0, 0, 0);

  /// <summary>The picked words, which the keyboard input method types.</summary>
  public string Words
  {
    [Pure]
    get => _editing.SelectedPiece is { Kind: OverlayPieceKind.Text } piece ? piece.Text : "";
    set => _actions.Words(value);
  }

  public void SetSwitch(bool value) => _actions.Switch(value);

  public void PressCompare(char op) => _actions.Press(op);

  [Pure]
  private static char PickedCompare(FormulaEditing editing) =>
    editing.Active && editing.SelectedPiece is { Kind: OverlayPieceKind.Compare } piece ? piece.Op : '\0';

  [Pure]
  private static bool? PickedBool(FormulaEditing editing) =>
    editing.SelectedPiece is { Kind: OverlayPieceKind.Bool } piece ? piece.Bool : null;

  // ---- Its parts ----

  public readonly KeypadViewModel Keypad;

  /// <summary>The values that fit where values go now.</summary>
  public readonly ValueBrowserViewModel Values;

  /// <summary>The line: the variable open and its pieces.</summary>
  public readonly LineViewModel Line;

  /// <summary>The picked condition's five slots.</summary>
  public readonly ConditionFormViewModel Form;

  /// <summary>The picked point among the nine, or Auto.</summary>
  public readonly GrowPickerViewModel Points;

  // ---- What the view reads ----

  /// <summary>Something is open in the dock and the keypad has it.</summary>
  public readonly Computed<bool> Typing;

  /// <summary>What the dock edits, such as "width" or "value:3"; null while nothing is.</summary>
  public readonly State<string> Owner = new(null);

  /// <summary>Apply was refused and what was in the way is still so: it is outlined until it is put right.</summary>
  public readonly State<bool> Alarmed = new(false);

  // ---- What the editor tells it ----

  /// <summary>
  /// What the dock edits now: <paramref name="owner"/> (null for nothing),
  /// named <paramref name="name"/> and <paramref name="whose"/> it is, with
  /// <paramref name="result"/> (already in its unit) on the right. The
  /// formula itself the dock reads from what the keypad types.
  /// </summary>
  public void Show(string owner, string name, string result, string whose = null)
  {
    Owner.Value = owner;
    Choice.Value = DockMethod.Rest;
    Values.Closed.Value = false;
    if (!_editing.Active || _editing.Problem == null)
      Alarmed.Value = false;
    Line.Alarmed = Alarmed.Value;
    Line.Show(owner != null ? _editing : null, name, result, whose);
    OverlayPiece picked = _editing.Active ? _editing.SelectedPiece : null;
    if (picked != null && picked.Kind == OverlayPieceKind.Condition)
      Form.Show(picked, _editing.SlotType(OverlayPiece.Result), _samples, _editing.Problem, Alarmed.Value);
    else if (picked != null && picked.Kind == OverlayPieceKind.Point)
      Points.Show(picked.Point, Anchor9.Center);
    Runtime.Probe();
  }

  /// <summary>
  /// The dock has a field open that is picked from a list of its own, in
  /// <paramref name="method"/>'s place: the line names it
  /// (<paramref name="whose"/> <paramref name="name"/>, marked
  /// <paramref name="glyph"/>) and shows <paramref name="label"/>, what it
  /// holds. No values are listed for it.
  /// </summary>
  public void ShowChoice(DockMethod method, string whose, string name, string glyph, string label)
  {
    Owner.Value = null;
    Choice.Value = method;
    Values.Closed.Value = true;
    Line.ShowChoice(whose, name, glyph, label);
    Runtime.Probe();
  }

  /// <summary>
  /// "No": what was asked can't be done as things are (Apply while a field
  /// can't be finished, a value that can't go where it was put). The line
  /// and the open condition shake, and what is in the way is outlined.
  /// </summary>
  public void Alarm()
  {
    Alarmed.Value = _editing.Active && _editing.Problem != null;
    Line.Shake.Play(_reduceMotion);
    Form.Shake.Play(_reduceMotion);
  }

  // What the picked piece is edited with; an empty variable has its type's.
  [Pure]
  private static DockMethod MethodOf(FormulaEditing editing, DockMethod choice)
  {
    // A field picked from its own list has the place while it is open, whatever is picked in the text.
    if (choice != DockMethod.Rest)
      return choice;
    if (!editing.Active)
      return DockMethod.Rest;
    OverlayPiece piece = editing.SelectedPiece;
    if (piece == null)
      return editing.Kind switch
      {
        OverlayValueType.Color => DockMethod.Color,
        OverlayValueType.Bool => DockMethod.Switch,
        OverlayValueType.Compare => DockMethod.Compare,
        OverlayValueType.Point => DockMethod.Points,
        _ => DockMethod.Keypad,
      };
    return piece.Kind switch
    {
      OverlayPieceKind.Text => DockMethod.Words,
      OverlayPieceKind.Bool => DockMethod.Switch,
      OverlayPieceKind.Compare => DockMethod.Compare,
      OverlayPieceKind.Point => DockMethod.Points,
      OverlayPieceKind.Condition => DockMethod.Condition,
      OverlayPieceKind.Value when piece.Value == OverlayValueId.MakeGradient => DockMethod.Gradient,
      OverlayPieceKind.Color => DockMethod.Color,
      OverlayPieceKind.Value when OverlayValueCatalog.Get(piece.Value).IsColor => DockMethod.Color,
      _ => DockMethod.Keypad,
    };
  }
}
