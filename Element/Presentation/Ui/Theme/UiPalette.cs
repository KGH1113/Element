using UnityEngine;

namespace Element.Presentation.Ui.Theme;

/// <summary>
/// Every named color of Element's screens: the dark surfaces, the text, the
/// roles drawn from the highlight color, and the error color.
/// </summary>
internal static class UiPalette
{
  public static readonly Color Background = new(0.067f, 0.067f, 0.067f, 1f);
  public static readonly Color Surface = new(0.115f, 0.115f, 0.115f, 1f);
  public static readonly Color Muted = new(0.66f, 0.66f, 0.66f, 1f);
  public static readonly Color Foreground = new(0.96f, 0.96f, 0.96f, 1f);

  /// <summary>The full-screen editors' floating bars.</summary>
  public static readonly Color Bar = new(.13f, .13f, .13f, .96f);

  /// <summary>The layout editor's toolbars.</summary>
  public static readonly Color Toolbar = new(.07f, .07f, .07f, 1);

  /// <summary>The add menu and its lists.</summary>
  public static readonly Color Menu = new(.105f, .105f, .105f);

  /// <summary>An icon whose button can't be pressed now.</summary>
  public static readonly Color Disabled = new(.4f, .4f, .4f);

  // Keep the chosen overlay color exact. UI roles have independent contrast floors.
  public static Color Accent => UiTheme.Accent;
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
  public static Color AccentSurface => Color.Lerp(Surface, SoftAccent, .13f);
  public static Color Action => Color.Lerp(SoftAccent, Accent, .2f);
  public static Color OnAction => Luminance(Action) > .179f ? new Color(.04f, .05f, .03f) : Color.white;

  /// <summary>Text that says what went wrong, and things that can't be used.</summary>
  public static readonly Color Error = new(1, .66f, .53f);

  private static float Luminance(Color color)
  {
    Color linear = color.linear;
    return linear.r * .2126f + linear.g * .7152f + linear.b * .0722f;
  }
}
