using Element.Configuration.Overlay;
using Element.Domain.Overlay.Variables;
using Element.Presentation.OverlayEditor.Parts.Formula;
using static Element.Presentation.Ui.Theme.UiLocale;

namespace Element.Presentation.OverlayEditor.Parts.ColorPicker;

/// <summary>What a color row holds, in a word: Condition, a value of yours, Highlight, Gradient, or its HEX.</summary>
internal static class ColorNames
{
  public static string Describe(OverlayColorSettings color)
  {
    if (color == null)
      return "";
    // A color that is more than one color names what it is.
    if (!string.IsNullOrEmpty(color.Formula))
      return Variable(OverlayVariable.Parse(color.Formula));
    if (color.IsGradient)
      return L("그라데이션", "Gradient");
    if (color.Highlight)
      return L("하이라이트", "Highlight");
    string hex = string.IsNullOrEmpty(color.Hex) ? "#FFFFFF" : color.Hex.ToUpperInvariant();
    return hex[0] == '#' ? hex : "#" + hex;
  }

  private static string Variable(OverlayVariable variable) =>
    variable.Count == 1 && variable[0].Kind == OverlayPieceKind.Custom
      ? "★" + OverlayCustomValues.Shown(variable[0].Text, FormulaWords.Current)
    : variable.Count == 1 && variable[0].Kind == OverlayPieceKind.Condition ? L("조건", "Condition")
    : L("값", "Value");
}
