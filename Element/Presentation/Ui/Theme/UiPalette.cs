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
  public static readonly Color Bar = new(.125f, .125f, .125f);

  /// <summary>
  /// What a full-screen editor's dimmed view reads as. Its controls are
  /// opaque surfaces worked out over it (<see cref="Wash"/>), so the overlay
  /// faint behind never shows through a control.
  /// </summary>
  public static readonly Color EditorDim = new(.004f, .004f, .004f);

  /// <summary>A control straight on an editor's dimmed view at rest: a property row, a value chip, a key.</summary>
  public static readonly Color EditorControl = Wash(EditorDim, .07f);

  /// <summary>An editor control that can't be pressed now: fainter than <see cref="EditorControl"/>.</summary>
  public static readonly Color EditorControlOff = Wash(EditorDim, .03f);

  /// <summary>A popover's panel: a font list, Grow from's points.</summary>
  public static readonly Color Popover = new(.09f, .09f, .09f);

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

  /// <summary>
  /// An opaque <paramref name="surface"/> lightened by a white wash of
  /// <paramref name="amount"/>: a control lighter than what it sits on,
  /// without letting what is behind show through.
  /// </summary>
  public static Color Wash(Color surface, float amount)
  {
    Color washed = Color.Lerp(surface, Color.white, amount);
    washed.a = 1;
    return washed;
  }

  private static float Luminance(Color color)
  {
    Color linear = color.linear;
    return linear.r * .2126f + linear.g * .7152f + linear.b * .0722f;
  }
}
