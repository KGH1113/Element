using System;

namespace Element.Configuration.Overlay;

// Every overlay color either follows the highlight color, keeping only its own
// opacity, or is a fixed custom color.
//
// A number the player can tie to live values keeps its plain value and, next
// to it, an optional formula (see OverlayFormula): values in braces with
// + - * /, such as "{progress-current}*3". An empty formula uses the number.
// Formulas give pixels as the game draws them at 1080p, or percent for opacity.
//
// With a gradient, its stops carry the colors and Hex/Alpha keep the first
// stop's, so a version without gradients still shows a sensible solid color.
// Highlight then applies to the whole gradient: every stop is the highlight
// color and only their opacities differ.
[Serializable]
[CopiedInPlace]
public sealed class OverlayColorSettings
{
  public bool Highlight;
  public string Hex = "#FFFFFF";
  public float Alpha = 1f;
  public string AlphaFormula = string.Empty;

  /// <summary>Null for a solid color.</summary>
  public OverlayGradient Gradient;

  public bool IsGradient => Gradient != null && Gradient.Stops != null && Gradient.Stops.Count > 1;

  public static OverlayColorSettings HighlightColor(float alpha = 1f) => new() { Highlight = true, Alpha = alpha };

  public static OverlayColorSettings Custom(string hex, float alpha = 1f) => new() { Hex = hex, Alpha = alpha };

  public OverlayColorSettings Clone() =>
    new()
    {
      Highlight = Highlight,
      Hex = Hex,
      Alpha = Alpha,
      AlphaFormula = AlphaFormula ?? string.Empty,
      Gradient = IsGradient ? Gradient.Clone() : null,
    };

  public bool SameAs(OverlayColorSettings other) =>
    other != null
    && Highlight == other.Highlight
    && IsGradient == other.IsGradient
    && (
      IsGradient
        ? Gradient.SameAs(other.Gradient, Highlight)
        : Alpha == other.Alpha && SameFormula(AlphaFormula, other.AlphaFormula) && (Highlight || Hex == other.Hex)
    );

  internal static bool SameFormula(string a, string b) => (a ?? string.Empty) == (b ?? string.Empty);
}
