namespace Element.Domain.Tweaks;

/// <summary>
/// How a planet part follows the player's planet appearance: the chosen color in place
/// of the game's when it is overridden, and the game's own opacity scaled by the chosen one.
/// The planets in the game and the preview in the settings both follow it.
/// </summary>
internal static class PlanetColorRule
{
  /// <summary>Whether the chosen color replaces the game's; a color that can't be read leaves the game's.</summary>
  public static bool UsesChosenColor(bool overrideColor, bool colorRead) => overrideColor && colorRead;

  /// <summary>The part's alpha: the game's, scaled by <paramref name="opacityPercent"/> held to 0–100.</summary>
  public static float Alpha(float gameAlpha, float opacityPercent)
  {
    float factor = opacityPercent / 100;
    if (factor < 0)
      factor = 0;
    else if (factor > 1)
      factor = 1;
    return gameAlpha * factor;
  }
}
