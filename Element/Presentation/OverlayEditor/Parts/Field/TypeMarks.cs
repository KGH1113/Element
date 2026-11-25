using System;
using System.Linq.Expressions;
using Element.Domain.Overlay.Variables;
using ElementUI;
using UnityEngine;
using static ElementUI.UI;
using Font = ElementUI.Font;

namespace Element.Presentation.OverlayEditor.Parts.Field;

/// <summary>
/// The mark that says what type a field or a value is: a small rounded tile
/// with a glyph in the type's soft color (# numbers blue, 가 words green,
/// ◐ true or false purple, ＊ anything), and for a color the color itself.
/// Every field row, piece and list header uses the same marks.
/// </summary>
internal static class TypeMarks
{
  private static readonly Color NumberInk = new(.49f, .7f, 1f),
    TextInk = new(.47f, .84f, .6f),
    BoolInk = new(.75f, .62f, 1f),
    PlainInk = new(.74f, .74f, .76f);

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
      OverlayValueType.Text => "가",
      OverlayValueType.Bool => "◐",
      OverlayValueType.Color => "●",
      OverlayValueType.Compare => "<>",
      _ => "＊",
    };

  /// <summary>A small tile with the type's glyph in its color.</summary>
  public static View Mark(OverlayValueType type, float size = 18)
  {
    Color ink = InkColor(type);
    Color tile = ink;
    tile.a = .16f;
    return Text(Glyph(type))
      .Font(Font.Caption.Sized(Mathf.Round(size * .62f)))
      .Foreground(ink)
      .LineLimit(1)
      .TextAlignment(TextAlign.Center)
      .Frame(width: size, height: size)
      .Background(tile, Mathf.Round(size * .28f));
  }

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
      One(OverlayValueType.Any, OverlayValueType.Compare)
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
