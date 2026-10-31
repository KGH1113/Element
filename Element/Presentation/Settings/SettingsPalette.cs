using Element.Presentation.Overlay.Shared;
using UnityEngine;

namespace Element.Presentation.Settings;

internal static class SettingsPalette
{
  // Keep the chosen overlay color exact. UI roles have independent contrast floors.
  public static Color Accent => OverlayTheme.Accent;
  public static Color SoftAccent
  {
    get
    {
      Color.RGBToHSV(Accent, out float hue, out float saturation, out float value);
      Color result = Color.HSVToRGB(hue, Mathf.Min(saturation, .42f), Mathf.Max(value, .85f));
      while (Luminance(result) < .45f)
        result = Color.Lerp(result, Color.white, .12f);
      return result;
    }
  }
  public static Color AccentSurface => Color.Lerp(SettingsUiFactory.Surface, SoftAccent, .13f);
  public static Color Action => Color.Lerp(SoftAccent, Accent, .2f);
  public static Color OnAction => Luminance(Action) > .179f ? new Color(.04f, .05f, .03f) : Color.white;

  private static float Luminance(Color color)
  {
    Color linear = color.linear;
    return linear.r * .2126f + linear.g * .7152f + linear.b * .0722f;
  }
}

internal static class SettingsLocale
{
  public static bool Korean => RDString.language == SystemLanguage.Korean;

  public static string L(string ko, string en) => Korean ? ko : en;
}
