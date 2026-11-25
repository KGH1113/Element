using Element.Domain.Overlay.Variables;
using Element.Presentation.Ui.Theme;
using ElementUI;
using UnityEngine;
using static ElementUI.UI;
using Font = ElementUI.Font;

namespace Element.Presentation.OverlayEditor.Parts.Field;

/// <summary>
/// The mark that says what type a field or a place takes, the same wherever
/// it shows: # for a number, 가 for words, ◐ for true or false, ✱ for
/// anything, ● for a color. Each type has its own soft tint (numbers blue,
/// words green, true or false purple; the rest neutral), so a mark reads at a
/// glance. A color value shows its own color instead (<see cref="Swatch"/>).
/// </summary>
internal static class TypeMarks
{
  private static readonly Color Number = new(.55f, .74f, 1f),
    Words = new(.56f, .86f, .6f),
    Bool = new(.78f, .64f, 1f),
    Neutral = new(.8f, .8f, .8f);

  /// <summary>The tint of <paramref name="type"/>'s mark: soft blue, green or purple, or a neutral grey.</summary>
  [Pure]
  public static UiColor Ink(OverlayValueType type) =>
    type switch
    {
      OverlayValueType.Number => Number,
      OverlayValueType.Text => Words,
      OverlayValueType.Bool => Bool,
      _ => Neutral,
    };

  /// <summary>The sign of <paramref name="type"/>: "#", "가", "◐", "✱", "●" for a color, "&lt;&gt;" for a comparison.</summary>
  [Pure]
  public static string Glyph(OverlayValueType type) =>
    type switch
    {
      OverlayValueType.Number => "#",
      OverlayValueType.Text => "가",
      OverlayValueType.Bool => "◐",
      OverlayValueType.Color => "●",
      OverlayValueType.Compare => "<>",
      _ => "✱",
    };

  /// <summary>
  /// A small rounded square, <paramref name="size"/> units wide, with
  /// <paramref name="type"/>'s sign in its tint over a faint wash of it.
  /// </summary>
  public static View Mark(OverlayValueType type, float size = 18)
  {
    Color ink = Tint(type);
    return UI.Text(Glyph(type))
      .Font(Font.Caption.Sized(Mathf.Round(size * .64f)).Bolded())
      .Foreground(Ink(type))
      .LineLimit(1)
      .Frame(width: size, height: size)
      .Background(Color.Lerp(UiPalette.EditorControl, ink, .2f), Mathf.Round(size * .28f))
      .AllowsHitTesting(false);
  }

  /// <summary>A round dot of <paramref name="color"/>, <paramref name="size"/> units wide, ringed so a dark color still shows.</summary>
  public static View Swatch(UiColor color, float size = 14) =>
    Circle()
      .Fill(color)
      .Frame(width: size - 3, height: size - 3)
      .Background(Circle().Fill(Colors.Muted).Frame(width: size, height: size))
      .Frame(width: size, height: size)
      .AllowsHitTesting(false);

  private static Color Tint(OverlayValueType type) =>
    type switch
    {
      OverlayValueType.Number => Number,
      OverlayValueType.Text => Words,
      OverlayValueType.Bool => Bool,
      _ => Neutral,
    };
}
