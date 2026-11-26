using System;
using System.Linq.Expressions;
using Element.Domain.Overlay.Variables;
using Element.Presentation.Ui.Declarative;
using Element.Presentation.Ui.Graphics;
using ElementUI;
using UnityEngine;
using static ElementUI.UI;
using Font = ElementUI.Font;

namespace Element.Presentation.OverlayEditor.Parts.Field;

/// <summary>
/// The mark that says what type a field or a value is: a glyph in the type's
/// soft color (# numbers blue, T words green, ◐ true or false purple,
/// * anything) on a small quiet tile, and for a color the color itself.
/// Every field row, piece and list header uses the same marks. Only the glyph
/// carries the color, so a column of marks stays calm.
/// </summary>
internal static class TypeMarks
{
  private static readonly Color NumberInk = new(.56f, .72f, 1f),
    TextInk = new(.58f, .86f, .65f),
    BoolInk = new(.79f, .68f, 1f),
    PlainInk = new(.85f, .85f, .87f),
    // Letters that are a word ("Aa") read as words do, in the foreground.
    WordInk = new(.95f, .95f, .96f);

  /// <summary>The soft color of <paramref name="type"/>: marks, underlines and the edge of its pieces.</summary>
  public static UiColor Ink(OverlayValueType type) => InkColor(type);

  /// <summary><see cref="Ink"/> as a plain color, for a mark drawn without a view.</summary>
  public static Color InkColor(OverlayValueType type) =>
    type switch
    {
      OverlayValueType.Number => NumberInk,
      OverlayValueType.Text => TextInk,
      OverlayValueType.Bool => BoolInk,
      _ => PlainInk,
    };

  /// <summary>The glyph of <paramref name="type"/>.</summary>
  public static string Glyph(OverlayValueType type) =>
    type switch
    {
      OverlayValueType.Number => "#",
      OverlayValueType.Text => "T",
      OverlayValueType.Bool => "◐",
      OverlayValueType.Color => "●",
      OverlayValueType.Compare => "<>",
      _ => "*",
    };

  /// <summary>A small quiet tile with the type's glyph in its color.</summary>
  public static View Mark(OverlayValueType type, float size = 18) =>
    Letter(type, size).Background(FieldLook.MarkTile, Mathf.Round(size * .3f));

  /// <summary>The type's glyph alone, as a small value in a field row leads with it.</summary>
  public static View Letter(OverlayValueType type, float size = 14) =>
    // The UI font has no asterisk that sits in the middle, and no sign for a comparison: they are drawn.
    type == OverlayValueType.Any
      ? Drawn(UiIconKind.AnyType, InkColor(type), size)
    : type == OverlayValueType.Compare ? Drawn(UiIconKind.CompareType, InkColor(type), size)
    : type == OverlayValueType.Point ? Drawn(UiIconKind.PlaceType, InkColor(type), size)
    : Letters(Glyph(type), InkColor(type), size);

  /// <summary>A mark for something that is no value type: "Aa" for a font.</summary>
  public static View Mark(string letters, float size = 18) =>
    Letter(letters, size).Background(FieldLook.MarkTile, Mathf.Round(size * .3f));

  /// <summary><see cref="Mark(string, float)"/> without its tile. Two letters take a little more room than one glyph.</summary>
  public static View Letter(string letters, float size = 14) =>
    letters.Length > 1
      ? Letters(letters, WordInk, size, .64f, Mathf.Round(size * 1.3f))
      : Letters(letters, PlainInk, size);

  private static View Letters(string letters, Color ink, float size, float scale = .66f, float width = 0) =>
    Text(letters)
      .Font(Font.Caption.Sized(Mathf.Round(size * scale)).Bolded())
      .Foreground(ink)
      .LineLimit(1)
      .TextAlignment(TextAlign.Center)
      .Frame(width: width > 0 ? width : size, height: size);

  private static View Drawn(UiIconKind icon, Color ink, float size) =>
    Image(ElementIcons.Of(icon), Mathf.Round(size * .72f)).Foreground(ink).Frame(width: size, height: size);

  /// <summary>
  /// The mark of a type that changes, as <paramref name="type"/> reads it
  /// from a view model: one tile shown at a time.
  /// </summary>
  public static View Mark(Expression<Func<OverlayValueType>> type, float size = 18)
  {
    View One(OverlayValueType shown, params OverlayValueType[] alike)
    {
      Expression test = Expression.Equal(type.Body, Expression.Constant(shown));
      foreach (OverlayValueType other in alike)
        test = Expression.OrElse(test, Expression.Equal(type.Body, Expression.Constant(other)));
      return If(Expression.Lambda<Func<bool>>(test), () => Mark(shown, size));
    }
    return ZStack(
      One(OverlayValueType.Number),
      One(OverlayValueType.Text),
      One(OverlayValueType.Bool),
      One(OverlayValueType.Color),
      One(OverlayValueType.Any),
      One(OverlayValueType.Compare),
      One(OverlayValueType.Point)
    );
  }

  /// <summary>A round dot of <paramref name="color"/>: a color value's mark.</summary>
  public static View Swatch(Color color, float size = 14)
  {
    Color shown = color;
    shown.a = 1;
    return Spacer().Frame(width: size, height: size).Background(shown, size / 2, new Color(1, 1, 1, .35f));
  }
}
