using Element.Configuration.Gameplay;
using Element.Presentation.Ui.Theme;
using ElementUI;
using UnityEngine;

namespace Element.Presentation.Settings.Pages;

/// <summary>
/// One part of a planet on the Planets page: its body, trail or ring. The
/// part takes the map's color or the player's own, with an opacity either way.
/// </summary>
internal sealed class PlanetPartModel : ViewModel
{
  public readonly PlanetComponentAppearance Appearance;

  /// <summary>The part's name on its row: Body, Trail or Ring.</summary>
  public readonly string Name;

  /// <summary>The color picker's title: Body color, Trail color or Ring color.</summary>
  public readonly string ColorTitle;

  /// <summary>Switches between the map's color and the player's own.</summary>
  public Command ToggleSource { get; }

  public PlanetPartModel(PlanetComponentAppearance appearance, string name, string colorTitle)
  {
    Appearance = appearance;
    Name = name;
    ColorTitle = colorTitle;
    ToggleSource = Command(() => Appearance.OverrideColor = !Appearance.OverrideColor);
  }

  /// <summary>
  /// The color and opacity the color picker shows and writes. A new color
  /// becomes the player's own; a new opacity alone keeps the map's color.
  /// </summary>
  public Color Color
  {
    // Reads only this part's appearance; a write probes what shows it.
    [Pure]
    get
    {
      if (!UiTheme.TryParseHighlight(Appearance.ColorHex, out Color color))
        color = UnityEngine.Color.white;
      color.a = Appearance.Opacity / 100;
      return color;
    }
    set
    {
      string hex = "#" + ColorUtility.ToHtmlStringRGB(value);
      if (Appearance.ColorHex != hex)
        Appearance.OverrideColor = true;
      Appearance.ColorHex = hex;
      Appearance.Opacity = Mathf.Round(value.a * 100);
    }
  }
}
