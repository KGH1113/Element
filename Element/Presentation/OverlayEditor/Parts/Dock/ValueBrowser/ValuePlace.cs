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
/// that goes there and which values fit. Some places take nothing from the
/// list (nothing open, an operator picked, a font being picked): the list is
/// then clear. Made afresh from what the dock has open (<see cref="Of"/>)
/// whenever that changes, and fixed once made.
/// </summary>
/// <remarks>
/// What fits is what the formula takes there (<see cref="FormulaEditing.CanDrop"/>),
/// less what a text has no use for: a text shows words and numbers, so a
/// place in it lists no colors and no true or false; and words are typed at
/// the caret, so the text itself lists no "Text" to make. A condition's
/// answer inside a text still does. A group (묶음) is listed only where a
/// calculation can be: for a number or anything, and not straight at the
/// caret, where a number starts the calculation.
/// </remarks>
internal sealed class ValuePlace
{
  private readonly Func<OverlayValueDefinition, bool> _fits;

  private ValuePlace(OverlayValueType type, Func<OverlayValueDefinition, bool> fits)
  {
    Type = type;
    _fits = fits;
  }

  /// <summary>The list shows values here.</summary>
  public bool Lists => _fits != null;

  /// <summary>What goes here, for the mark over the list.</summary>
  public OverlayValueType Type { get; }

  /// <summary>Whether <paramref name="value"/> can go here; never while the list shows nothing.</summary>
  public bool Fits(OverlayValueDefinition value) => _fits != null && _fits(value);

  /// <summary>
  /// The place <paramref name="editing"/> has open: the innermost variable,
  /// or with nothing open, the text's caret when <paramref name="caret"/>
  /// (values go in there), else nowhere. While <paramref name="closed"/> (a
  /// field picked from its own list is open), nothing is listed.
  /// </summary>
  [Pure]
  public static ValuePlace Of(FormulaEditing editing, bool caret, bool closed)
  {
    if (closed)
      return new ValuePlace(OverlayValueType.Any, null);
    if (!editing.Active)
      return new ValuePlace(
        OverlayValueType.Any,
        caret
          // At the caret 숫자 starts a calculation; a group goes inside one.
          ? value => InText(value) && value.Id != OverlayValueId.MakeText && value.Id != OverlayValueId.MakeGroup
          : null
      );
    OverlayValueType type = editing.Kind;
    // A sign between numbers is changed on the keypad.
    if (editing.SelectedPiece?.Kind == OverlayPieceKind.Operator)
      return new ValuePlace(type, null);
    bool text = editing.Path.Count == 1 && editing.Slot.IsText;
    bool calculates = type == OverlayValueType.Number || type == OverlayValueType.Any;
    return new ValuePlace(
      type,
      value =>
        editing.CanDrop(value)
        && (type != OverlayValueType.Any || InText(value))
        && !(text && value.Id == OverlayValueId.MakeText)
        && (calculates || value.Id != OverlayValueId.MakeGroup)
    );
  }

  // A text shows words and numbers: no colors, no true or false, no comparison, no point.
  private static bool InText(OverlayValueDefinition value)
  {
    OverlayValueType type = OverlayVariableTypes.TypeOf(value);
    return type == OverlayValueType.Number || type == OverlayValueType.Text || type == OverlayValueType.Any;
  }
}
