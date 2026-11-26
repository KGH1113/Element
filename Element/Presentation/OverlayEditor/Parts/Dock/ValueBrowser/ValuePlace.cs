using System;
using System.Collections.Generic;
using Element.Domain.Overlay.Values;
using Element.Domain.Overlay.Variables;
using Element.Presentation.OverlayEditor.Parts.Formula;
using ElementUI;
using static Element.Presentation.Ui.Theme.UiLocale;

namespace Element.Presentation.OverlayEditor.Parts.Dock.ValueBrowser;

/// <summary>
/// Where the dock puts values now, as its list of values needs it: the type
/// that goes there, the words over the list ("숫자 칸에 넣을 수 있는 값"),
/// and which values fit. Two places take nothing from the list: none at all
/// while nothing is open, and a sign, which the input method picks; each has
/// a hint instead (<see cref="Hint"/>). Made afresh from what the dock has
/// open (<see cref="Of"/>) whenever that changes, and fixed once made.
/// </summary>
/// <remarks>
/// What fits is what the formula takes there (<see cref="FormulaEditing.CanDrop"/>),
/// less what a text has no use for: a text shows words and numbers, so a
/// place in it lists no colors and no true or false; and words are typed at
/// the caret, so the text itself lists no "Text" to make. A condition's
/// answer inside a text still does. A group (묶음) is listed only where a
/// calculation can be: not for a color or a true-or-false, and not straight
/// at the caret, where a number starts the calculation.
/// </remarks>
internal sealed class ValuePlace
{
  private readonly Func<OverlayValueDefinition, bool> _fits;

  private ValuePlace(OverlayValueType type, string title, string hint, Func<OverlayValueDefinition, bool> fits)
  {
    Type = type;
    Title = title ?? "";
    Hint = hint ?? "";
    _fits = fits;
  }

  /// <summary>The list shows values here; while it doesn't, <see cref="Hint"/> says why.</summary>
  public bool Lists => _fits != null;

  /// <summary>What goes here, for the mark beside <see cref="Title"/>.</summary>
  public OverlayValueType Type { get; }

  /// <summary>Over the list: "숫자 칸에 넣을 수 있는 값", "결과값 자리에 넣을 수 있는 값"; empty while it lists nothing.</summary>
  public string Title { get; }

  /// <summary>In place of the list while it lists nothing: what to do; empty while it lists values.</summary>
  public string Hint { get; }

  /// <summary>Whether <paramref name="value"/> can go here; never while the list shows nothing.</summary>
  public bool Fits(OverlayValueDefinition value) => _fits != null && _fits(value);

  /// <summary>
  /// The place <paramref name="editing"/> has open: the innermost variable,
  /// or with nothing open, the text's caret when <paramref name="caret"/>
  /// (values go in there), else nowhere. While a field picked from a fixed
  /// set is open, <paramref name="choice"/> says so and nothing is listed.
  /// </summary>
  [Pure]
  public static ValuePlace Of(FormulaEditing editing, bool caret, string choice)
  {
    // A font or a point is picked on the input method: the game gives no such values.
    if (!string.IsNullOrEmpty(choice))
      return new ValuePlace(OverlayValueType.Any, null, choice, null);
    if (!editing.Active)
      return caret
        ? new ValuePlace(
          OverlayValueType.Any,
          TextTitle(),
          null,
          // At the caret 숫자 starts a calculation; a group goes inside one.
          value => InText(value) && value.Id != OverlayValueId.MakeText && value.Id != OverlayValueId.MakeGroup
        )
        : new ValuePlace(
          OverlayValueType.Any,
          null,
          L(
            "값이나 칸을 고르면 거기에 넣을 수 있는 값만 여기 나와요.",
            "Pick a value or a field, and the values that fit there show here."
          ),
          null
        );
    OverlayValueType type = editing.Kind;
    if (type == OverlayValueType.Compare || editing.SelectedPiece?.Kind == OverlayPieceKind.Operator)
      return new ValuePlace(
        type,
        null,
        L("기호는 오른쪽 입력기에서 골라요.", "Pick the sign on the input method at the right."),
        null
      );
    IReadOnlyList<FormulaFrame> path = editing.Path;
    bool text = path.Count == 1 && editing.Slot.IsText;
    return new ValuePlace(
      type,
      text ? TextTitle() : TitleOf(path[path.Count - 1]),
      null,
      value =>
        editing.CanDrop(value)
        && (type != OverlayValueType.Any || InText(value))
        && !(text && value.Id == OverlayValueId.MakeText)
        // A group holds a calculation: a color or a true-or-false has none to hold.
        && !(value.Id == OverlayValueId.MakeGroup && type is OverlayValueType.Color or OverlayValueType.Bool)
    );
  }

  // A text shows words and numbers: no colors, no true or false.
  private static bool InText(OverlayValueDefinition value)
  {
    OverlayValueType type = OverlayVariableTypes.TypeOf(value);
    return type != OverlayValueType.Color && type != OverlayValueType.Bool;
  }

  private static string TextTitle() => L("커서 자리에 넣을 수 있는 값", "Values for the caret's place");

  // A condition's slot by its name, anything else by its type.
  private static string TitleOf(FormulaFrame frame)
  {
    if (frame.Owner?.Kind == OverlayPieceKind.Condition)
    {
      string slot = FormulaRefusals.SlotName(frame.Slot);
      return L(slot + " 자리에 넣을 수 있는 값", "Values for the " + slot + " slot");
    }
    return frame.Type switch
    {
      OverlayValueType.Number => L("숫자 칸에 넣을 수 있는 값", "Values for a number field"),
      OverlayValueType.Text => L("글자 칸에 넣을 수 있는 값", "Values for a text field"),
      OverlayValueType.Color => L("색 칸에 넣을 수 있는 값", "Values for a color field"),
      OverlayValueType.Bool => L("참·거짓 칸에 넣을 수 있는 값", "Values for a true-or-false field"),
      _ => L("아무 칸에 넣을 수 있는 값", "Values for an any-type field"),
    };
  }
}
