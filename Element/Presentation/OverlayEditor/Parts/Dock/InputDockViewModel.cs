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
  /// <param name="caret">With nothing open, values go in at a text's caret, and the list shows what fits there.</param>
  public InputDockViewModel(
    OverlayValues values,
    FormulaEditing editing,
    EditorFrameViewModel notices,
    IInputDockActions actions,
    bool reduceMotion,
    bool caret = false
  )
  {
    _editing = editing;
    _actions = actions;
    _emptyHint = L("칸이나 값을 누르면 여기서 고쳐요", "Click a field or a value to edit it here");
    Notices = notices;
    // A text is its own content field, open all along: there is always something to apply.
    Keypad = Own(new KeypadViewModel(actions, editing, Choice, caret));
    Values = Own(new ValueBrowserViewModel(values, actions, editing, caret));
    Line = Own(new LineViewModel(actions));
    Typing = Computed(() => Owner.Value != null && _editing.Active);
    Problem = Computed(() => Typing.Value ? FormulaRefusals.Explain(_editing.Problem) ?? "" : "");
    RestNote = Computed(() => Said(RestTitle.Value, RestBody.Value));
    Method = Computed(() => MethodOf(_editing, Choice.Value));
    MethodName = Computed(() => NameOf(MethodOf(_editing, Choice.Value)));
    SwitchOn = Computed(() => PickedBool(_editing));
    CompareOn = Computed(() => PickedCompare(_editing));
    Color rest = UiPalette.EditorControl;
    Rest = rest;
    Lit = Color.Lerp(rest, UiPalette.SoftAccent, .34f);
    Warn = Color.Lerp(rest, UiPalette.Error, .22f);
    AccentInk = UiPalette.SoftAccent;
    OnInk = UiPalette.Foreground;
  }

  // ---- The input method ----

  /// <summary>What the picked piece is edited with.</summary>
  public readonly Computed<DockMethod> Method;

  /// <summary>Over the input method's place: "입력기 · 키패드", or "입력기" alone at rest.</summary>
  public readonly Computed<string> MethodName;

  /// <summary>
  /// The field open that is picked from a fixed set (a font, a point) and so
  /// has no values on the line: its input method; <see cref="DockMethod.Rest"/> for none.
  /// </summary>
  public readonly State<DockMethod> Choice = new(DockMethod.Rest);

  /// <summary>The input method's place at rest: what it says first, and what it goes on to say.</summary>
  public readonly State<string> RestTitle = new(L("값을 누르면", "Pick a value")),
    RestBody =
      new(
        L(
          "그 값의 입력기가 여기 나와요. 숫자는 키패드, 글자는 키보드, 색은 색 고르기, 참·거짓은 스위치예요.",
          "and its input method shows here. Numbers have the keypad, words the keyboard, colors the color picker, true or false a switch."
        )
      );

  /// <summary>What the place says at rest, as one rich text (<see cref="Said"/>).</summary>
  public readonly Computed<string> RestNote;

  /// <summary>
  /// A note of the input method's place as rich text: <paramref name="lead"/>
  /// bold in the foreground, <paramref name="rest"/> going on after it.
  /// </summary>
  [Pure]
  public static string Said(string lead, string rest) =>
    string.IsNullOrEmpty(lead) ? rest ?? "" : "<b><color=#F3F3F4>" + lead + "</color></b> " + rest;

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

  /// <summary>The values that fit where values go now: the places, the open one's chips and the search.</summary>
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
  /// <summary>
  /// What the input method's place says while nothing is picked: by default
  /// that a picked value's input method shows there; a text editor says words
  /// are typed where the caret is.
  /// </summary>
  public void AtRest(string title, string body)
  {
    RestTitle.Value = title ?? "";
    RestBody.Value = body ?? "";
  }

  public void Show(string owner, string name, string result, string hint, string otherwise = null, string whose = null)
  {
    Owner.Value = owner;
    Choice.Value = DockMethod.Rest;
    Values.Choice.Value = "";
    Line.Show(owner != null ? _editing : null, name, result, string.IsNullOrEmpty(hint) ? _emptyHint : hint, whose);
    Runtime.Probe();
  }

  /// <summary>
  /// The dock has a field open that is picked from a fixed set, in
  /// <paramref name="method"/>'s place: the line names it
  /// (<paramref name="whose"/> <paramref name="name"/>, marked
  /// <paramref name="glyph"/>) and shows <paramref name="label"/>, what it
  /// holds; where values are listed, <paramref name="hint"/> says they are
  /// picked on the right.
  /// </summary>
  public void ShowChoice(DockMethod method, string whose, string name, string glyph, string label, string hint)
  {
    Owner.Value = null;
    Choice.Value = method;
    Values.Choice.Value = hint ?? "";
    Line.ShowChoice(whose, name, glyph, label);
    Runtime.Probe();
  }

  [Pure]
  private static string NameOf(DockMethod method)
  {
    string which = method switch
    {
      DockMethod.Keypad => L("키패드", "Keypad"),
      DockMethod.Words => L("키보드", "Keyboard"),
      DockMethod.Switch => L("스위치", "Switch"),
      DockMethod.Compare => L("비교 기호", "Comparison"),
      DockMethod.Condition => L("조건", "Condition"),
      DockMethod.Group => L("계산", "Calculation"),
      DockMethod.Color => L("색 고르기", "Color picker"),
      DockMethod.Gradient => L("그라데이션", "Gradient"),
      DockMethod.Fonts => L("폰트 목록", "Fonts"),
      DockMethod.Points => L("아홉 자리", "Nine points"),
      _ => null,
    };
    return which == null ? L("입력기", "Input method") : L("입력기 · ", "Input method · ") + which;
  }

  // What the picked piece is edited with; the operator slot has its comparisons.
  [Pure]
  private static DockMethod MethodOf(FormulaEditing editing, DockMethod choice)
  {
    // A field picked from a fixed set has the place while it is open, whatever is picked in the text.
    if (choice != DockMethod.Rest)
      return choice;
    if (!editing.Active)
      return DockMethod.Rest;
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
      OverlayPieceKind.Value when piece.Value == OverlayValueId.MakeGradient => DockMethod.Gradient,
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
