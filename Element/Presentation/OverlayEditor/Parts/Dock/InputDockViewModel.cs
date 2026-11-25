using System.Collections.Generic;
using System.Text;
using Element.Domain.Overlay.Values;
using Element.Domain.Overlay.Variables;
using Element.Presentation.Overlay.Text;
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
  private readonly IInputDockActions _actions;
  private readonly Timeline _blink = Timeline.Periodic(BlinkSeconds);
  private readonly bool _steadyCaret;
  private readonly string _emptyHint;
  private readonly State<string> _name = new(""),
    _result = new(""),
    _hint = new(""),
    _otherwise = new("");
  private DropState _drop;

  /// <param name="values">What the overlay shows now, for the chips; null shows names only.</param>
  /// <param name="editing">The formula the keypad types; the line shows it as it changes.</param>
  /// <param name="notices">The editor's notices, which the dock shows above its line.</param>
  /// <param name="actions">Where everything the player does in the dock goes.</param>
  /// <param name="reduceMotion">The caret stays on instead of blinking.</param>
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
    _steadyCaret = reduceMotion;
    _emptyHint = L("숫자를 누르거나 값을 고르세요", "Type a number or pick a value");
    Notices = notices;
    Keypad = Own(new KeypadViewModel(actions, editing));
    Values = Own(new ValueBrowserViewModel(values, actions, editing, caret));
    Typing = Computed(() => Owner.Value != null && _editing.Active);
    // Where the dock is: the field, then each slot opened inside it.
    Title = Computed(() => Typing.Value ? PathText(_editing, _name.Value) + "  =" : _hint.Value);
    TitleInk = Computed(() => Typing.Value ? Muted : Resting);
    FormulaEmpty = Computed(() => Typing.Value && IsEmpty(_editing));
    // A color that is a gradient shows it where its pieces would be.
    FormulaInk = Computed(() => FormulaEmpty.Value && !ShowsPlaceholder() ? Placeholder : Ink);
    Formula = Computed(() =>
      !Typing.Value ? ""
      : FormulaEmpty.Value ? (ShowsPlaceholder() ? _otherwise.Value : _emptyHint)
      : Describe(_editing)
    );
    Result = Computed(() => Typing.Value && _result.Value.Length > 0 ? "= " + _result.Value : "");
    Problem = Computed(() => Typing.Value ? FormulaRefusals.Explain(_editing.Problem) ?? "" : "");
    Method = Computed(() => MethodOf(_editing));
    SwitchOn = Computed(() => PickedBool(_editing));
    CompareOn = Computed(() => PickedCompare(_editing));
    SlotText = new Computed<string>[5];
    SlotEmpty = new Computed<bool>[5];
    for (int i = 0; i < 5; i++)
    {
      int slot = i;
      SlotText[i] = Computed(() => SlotWords(_editing, slot));
      SlotEmpty[i] = Computed(() => SlotMissing(_editing, slot));
    }
    Color rest = UiPalette.EditorControl;
    Rest = rest;
    Lit = Color.Lerp(rest, UiPalette.SoftAccent, .34f);
    Warn = Color.Lerp(rest, UiPalette.Error, .22f);
    AccentInk = UiPalette.SoftAccent;
    OnInk = UiPalette.Foreground;

    // While nothing is typed the caret doesn't read the blink, which then rests.
    Caret = Computed(() => Typing.Value && (_steadyCaret || _blink.Even));
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

  /// <summary>A condition's slots, or a group's one, in words (rich text).</summary>
  public readonly Computed<string>[] SlotText;

  /// <summary>A slot still empty where something must be.</summary>
  public readonly Computed<bool>[] SlotEmpty;

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

  public void EnterSlot(int slot) => _actions.Enter(slot);

  /// <summary>A piece of the line clicked: picked, or let go.</summary>
  public void SelectPiece(int index) => _actions.Select(index);

  // The operator in a condition's operator slot; '\0' for none.
  [Pure]
  private static char PickedCompare(FormulaEditing editing) =>
    editing.Active && editing.Kind == OverlayValueType.Compare && editing.Working.Count == 1
      ? editing.Working[0].Op
      : '\0';

  [Pure]
  private static bool? PickedBool(FormulaEditing editing) =>
    editing.SelectedPiece is { Kind: OverlayPieceKind.Bool } piece ? piece.Bool : null;

  [Pure]
  private bool ShowsPlaceholder() => _otherwise.Value.Length > 0 && _editing.Path.Count == 1;

  // ---- Its parts ----

  public readonly KeypadViewModel Keypad;

  /// <summary>The values that fit where values go now: the places, the open one's chips and the search.</summary>
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
  /// reads from what the keypad types; <paramref name="otherwise"/> is what
  /// shows when none of its cases holds (rich text), said after them.
  /// </summary>
  public void Show(string owner, string name, string result, string hint, string otherwise = null)
  {
    Owner.Value = owner;
    _name.Value = name ?? "";
    _result.Value = result ?? "";
    _hint.Value = hint ?? "";
    _otherwise.Value = otherwise ?? "";
  }

  /// <summary>The editor drew afresh after the player did something: the caret shows at once and blinks from there.</summary>
  public void RestartBlink() => _blink.Restart();

  // ---- A value carried onto the formula line ----

  /// <summary>
  /// Whether <paramref name="value"/> may join the formula: nothing is asked
  /// while nothing is edited, and words can't go where a number does.
  /// </summary>
  public DropState OverFormula(OverlayValueDefinition value) =>
    Owner.Value == null ? DropState.None
    : _editing.Active && _editing.CanDrop(value) ? DropState.Accepts
    : DropState.Refuses;

  /// <summary>Let go on the line: the editor joins it to the formula, or says why it can't.</summary>
  public void DropOnFormula(OverlayValueDefinition value) => _actions.DropOnFormula(value);

  /// <summary>Whether a carried value is over the formula line, and whether it can join the formula.</summary>
  public void SetDrop(DropState drop)
  {
    if (_drop == drop)
      return;
    _drop = drop;
    Underline.Value =
      drop == DropState.Accepts ? UiPalette.SoftAccent
      : drop == DropState.Refuses ? UiPalette.Error
      : Line;
  }

  protected override void OnDisposed() => _blink.Dispose();

  [Pure]
  private static bool IsEmpty(FormulaEditing editing) => editing.Working == null || editing.Working.IsEmpty;

  // The pieces as they read, each a link the line finds again on a click:
  // values as soft chips, words in quotes, operators spaced out, a condition
  // in a sentence, a group in brackets; the picked piece lit. Made again only
  // when the variable changes, not when the editor draws again.
  [Pure]
  private static string Describe(FormulaEditing editing)
  {
    OverlayVariable variable = editing.Working;
    if (variable == null)
      return "";
    StringBuilder builder = Builder.Clear();
    int picked = editing.Selected;
    for (int i = 0; i < variable.Count; i++)
    {
      OverlayPiece piece = variable[i];
      bool on = i == picked;
      builder.Append("<link=").Append(i).Append('>');
      if (piece.Kind == OverlayPieceKind.Operator)
      {
        builder.Append(on ? "  <u>" : "  ").Append(OverlayVariable.Symbol(piece.Op)).Append(on ? "</u>  " : "  ");
        builder.Append("</link>");
        continue;
      }
      if (i > 0 && variable[i - 1].Kind != OverlayPieceKind.Operator)
        builder.Append(' ');
      builder.Append(on ? "<mark=#FFFFFF5C> " : "<mark=#FFFFFF1F> ");
      Piece(builder, piece);
      builder.Append(" </mark></link>");
    }
    return builder.ToString();
  }

  /// <summary>One piece in the line's words: a value's name, words in quotes, a color's swatch, a condition in brief.</summary>
  public static void Piece(StringBuilder builder, OverlayPiece piece, int depth = 0)
  {
    OverlayVariableWords words = FormulaWords.Current;
    switch (piece.Kind)
    {
      case OverlayPieceKind.Number:
        builder.Append(piece.Text.Replace('-', '−'));
        break;
      case OverlayPieceKind.Text:
        builder.Append('“');
        OverlayTextComposer.AppendLiteral(builder, piece.Text);
        builder.Append('”');
        break;
      case OverlayPieceKind.Color:
        Swatch(builder, OverlayTextComposer.ParseHex(piece.Text)).Append(piece.Text);
        break;
      case OverlayPieceKind.Bool:
        builder.Append(piece.Bool ? words.Yes : words.No);
        break;
      case OverlayPieceKind.Compare:
      case OverlayPieceKind.Operator:
        OverlayTextComposer.AppendLiteral(builder, OverlayVariable.Symbol(piece.Op).ToString());
        break;
      case OverlayPieceKind.Value:
        OverlayValueDefinition value = OverlayValueCatalog.Get(piece.Value);
        if (value.IsColor)
          Swatch(builder, UiTheme.Accent);
        builder.Append(ValueSectionNames.ValueName(value).Replace("<", ""));
        break;
      case OverlayPieceKind.Custom:
        builder.Append('★').Append(OverlayCustomValues.Shown(piece.Text, words).Replace("<", ""));
        break;
      case OverlayPieceKind.Condition:
        builder.Append(words.ConditionStart).Append(' ');
        Brief(builder, piece.Slots[OverlayPiece.Left], depth);
        builder.Append(' ');
        Brief(builder, piece.Slots[OverlayPiece.Compare], depth);
        builder.Append(' ');
        Brief(builder, piece.Slots[OverlayPiece.Right], depth);
        builder.Append(' ').Append(words.Then).Append(' ');
        Brief(builder, piece.Slots[OverlayPiece.Result], depth);
        builder.Append(" · ").Append(words.Otherwise).Append(' ');
        Brief(builder, piece.Slots[OverlayPiece.Default], depth);
        break;
      case OverlayPieceKind.Group:
        builder.Append('(');
        Brief(builder, piece.Slots[0], depth);
        builder.Append(')');
        break;
    }
  }

  // A slot in brief: its pieces, or an empty box.
  private static void Brief(StringBuilder builder, OverlayVariable slot, int depth)
  {
    if (slot.IsEmpty || depth > 2)
    {
      builder.Append(slot.IsEmpty ? "□" : "…");
      return;
    }
    for (int i = 0; i < slot.Count; i++)
    {
      if (i > 0)
        builder.Append(' ');
      Piece(builder, slot[i], depth + 1);
    }
  }

  // A slot of the picked condition or group, in words for the input method's rows.
  [Pure]
  private static string SlotWords(FormulaEditing editing, int slot)
  {
    OverlayPiece piece = editing.SelectedPiece;
    OverlayVariable inner = Inside(piece, slot);
    if (inner == null)
      return "";
    if (inner.IsEmpty)
      return slot >= OverlayPiece.Result && editing.Kind == OverlayValueType.Any
        ? L("비어 있음 · 아무것도 안 보여요", "Empty · shows nothing")
        : L("비어 있음", "Empty");
    StringBuilder builder = new();
    for (int i = 0; i < inner.Count; i++)
    {
      if (i > 0)
        builder.Append(' ');
      Piece(builder, inner[i], 1);
    }
    return builder.ToString();
  }

  // A slot still empty where something must be: a result or default may be empty only in words.
  [Pure]
  private static bool SlotMissing(FormulaEditing editing, int slot) =>
    Inside(editing.SelectedPiece, slot) is { IsEmpty: true }
    && !(
      editing.SelectedPiece.Kind == OverlayPieceKind.Condition
      && slot >= OverlayPiece.Result
      && editing.Kind == OverlayValueType.Any
    );

  // The variable in slot <paramref name="slot"/> of a piece: a condition's or group's, or a made value's.
  [Pure]
  private static OverlayVariable Inside(OverlayPiece piece, int slot)
  {
    if (piece == null)
      return null;
    if (piece.Kind == OverlayPieceKind.Custom)
      return slot == 0 && OverlayCustomValues.TryGet(piece.Text, out OverlayVariable made) ? made : null;
    return slot < piece.Slots.Length ? piece.Slots[slot] : null;
  }

  // The path as the dock's name reads it: "Width › Result".
  [Pure]
  private static string PathText(FormulaEditing editing, string name)
  {
    IReadOnlyList<FormulaFrame> path = editing.Path;
    if (path.Count <= 1)
      return name;
    StringBuilder builder = new(name);
    for (int i = 1; i < path.Count; i++)
    {
      builder.Append("  ›  ");
      if (path[i].Custom != null)
        builder.Append('★').Append(OverlayCustomValues.Shown(path[i].Custom, FormulaWords.Current).Replace("<", ""));
      else
        builder.Append(
          path[i].Owner?.Kind == OverlayPieceKind.Group
            ? FormulaRefusals.SlotName(-1)
            : FormulaRefusals.SlotName(path[i].Slot)
        );
    }
    return builder.ToString();
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
