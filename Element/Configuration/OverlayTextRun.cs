using System;

namespace Element.Configuration;

// One run is either literal text in one color, or one value: Value is a
// formula, usually a single value's key ("combo"), or values with numbers and
// + - * / ("{combo}*2").
[Serializable]
[CopiedInPlace]
public sealed class OverlayTextRun
{
  public string Text = string.Empty;
  public string Value = string.Empty;
  public OverlayColorSettings Color = new();

  // Font size in reference pixels before the element's scale; zero is the
  // text's base size. SizeFormula, when set, gives pixels as drawn instead.
  public float Size;
  public string SizeFormula = string.Empty;

  // An OverlayFontKey; empty follows the overlay's default font.
  public string Font = string.Empty;

  // A value run grows briefly each time what it shows changes. Literal text
  // shows no live value, so it never moves.
  public bool Animate;

  public bool IsValue => !string.IsNullOrEmpty(Value);

  public static OverlayTextRun Literal(string text, OverlayColorSettings color) =>
    new() { Text = text ?? string.Empty, Color = color };

  public static OverlayTextRun ValueRun(string key, OverlayColorSettings color) => new() { Value = key, Color = color };

  public OverlayTextRun Clone() =>
    new()
    {
      Text = Text,
      Value = Value,
      Color = (Color ?? new OverlayColorSettings()).Clone(),
      Size = Size,
      SizeFormula = SizeFormula ?? string.Empty,
      Font = Font ?? string.Empty,
      Animate = Animate,
    };

  public bool SameAs(OverlayTextRun other) =>
    other != null
    && Text == other.Text
    && Value == other.Value
    && Animate == other.Animate
    && Size == other.Size
    && OverlayColorSettings.SameFormula(SizeFormula, other.SizeFormula)
    && OverlayColorSettings.SameFormula(Font, other.Font)
    && (Color ?? new OverlayColorSettings()).SameAs(other.Color ?? new OverlayColorSettings());
}
