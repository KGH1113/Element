using Element.Configuration.Overlay;
using static Element.Presentation.Ui.Theme.UiLocale;

namespace Element.Presentation.Layout.ContentEditors.Shell;

/// <summary>What a color row holds, in a word: By value, Highlight, Gradient, or its HEX.</summary>
internal static class OverlayColorNames
{
  public static string Describe(OverlayColorSettings color)
  {
    if (color == null)
      return "";
    if (color.FollowsValue)
      return L("값에 따라", "By value");
    if (color.IsGradient)
      return L("그라데이션", "Gradient");
    if (color.Highlight)
      return L("하이라이트", "Highlight");
    string hex = string.IsNullOrEmpty(color.Hex) ? "#FFFFFF" : color.Hex.ToUpperInvariant();
    return hex[0] == '#' ? hex : "#" + hex;
  }
}
