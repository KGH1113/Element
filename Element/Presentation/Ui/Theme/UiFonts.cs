using TMPro;

namespace Element.Presentation.Ui.Theme;

/// <summary>The font the settings window and the layout editor draw with.</summary>
internal static class UiFonts
{
  /// <summary>The overlay's bundled font; without it, the game's font for its language.</summary>
  public static TMP_FontAsset Choose(TMP_FontAsset overlayFont) =>
    overlayFont
    ?? (UiLocale.Korean ? RDConstants.data.koreanFontTMPro : RDConstants.data.latinFontTMPro)
    ?? TMP_Settings.defaultFontAsset;
}
