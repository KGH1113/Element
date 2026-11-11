using Element.Configuration;

namespace Element.Domain.Elements;

// One caret step: a displayed character (one or two UTF-16 units) or a whole
// value, with its color, size (zero for the text's base size) and font (an
// OverlayFontKey; empty follows the overlay's default font). A value is a
// saved formula (OverlayFormula), usually one value's key. A size formula,
// when set, gives the size in pixels as drawn and replaces Size. Only a value
// animates: it grows briefly each time what it shows changes.
internal readonly struct OverlayTextItem
{
  public readonly string Text;
  public readonly string Value;
  public readonly OverlayColorSettings Color;
  public readonly float Size;
  public readonly string SizeFormula;
  public readonly string Font;
  public readonly bool Animate;
  public bool IsValue => Value != null;

  private OverlayTextItem(
    string text,
    string value,
    OverlayColorSettings color,
    float size,
    string sizeFormula,
    string font,
    bool animate
  )
  {
    Text = text;
    Value = value;
    Color = color;
    Size = size;
    SizeFormula = sizeFormula ?? string.Empty;
    Font = font ?? string.Empty;
    Animate = value != null && animate;
  }

  public static OverlayTextItem Character(
    string text,
    OverlayColorSettings color,
    float size = 0,
    string sizeFormula = null,
    string font = null
  ) => new(text, null, color, size, sizeFormula, font, false);

  public static OverlayTextItem ValueItem(
    string formula,
    OverlayColorSettings color,
    float size = 0,
    string sizeFormula = null,
    string font = null,
    bool animate = false
  ) => new(null, formula, color, size, sizeFormula, font, animate);

  public OverlayTextItem WithColor(OverlayColorSettings color) =>
    new(Text, Value, color, Size, SizeFormula, Font, Animate);

  public OverlayTextItem WithSize(float size, string sizeFormula = null) =>
    new(Text, Value, Color, size, sizeFormula, Font, Animate);

  public OverlayTextItem WithFont(string font) => new(Text, Value, Color, Size, SizeFormula, font, Animate);

  public OverlayTextItem WithValue(string formula) => new(Text, formula, Color, Size, SizeFormula, Font, Animate);

  public OverlayTextItem WithAnimate(bool animate) => new(Text, Value, Color, Size, SizeFormula, Font, animate);

  public bool SameStyle(OverlayTextItem other) =>
    Size == other.Size
    && SizeFormula == other.SizeFormula
    && Font == other.Font
    && Animate == other.Animate
    && Color.SameAs(other.Color);
}
