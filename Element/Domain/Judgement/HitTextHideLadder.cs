using Element.Configuration;

namespace Element.Domain.Judgement;

/// <summary>
/// ADOFAI's Hide Judgment Text is one list of steps; each step hides everything the
/// step before it hides, plus more. Maps the mod's per-judgment choices to it.
/// </summary>
internal static class HitTextHideLadder
{
  public static bool IsKnown(HitMarginHidePreset step) =>
    step >= HitMarginHidePreset.None && step <= HitMarginHidePreset.HideAll;

  /// <summary>The largest step whose judgments the mod hides as well.</summary>
  public static HitMarginHidePreset Covered(JudgementVisualOptions options)
  {
    if (!options.HideXPerfect)
      return HitMarginHidePreset.None;
    if (!options.HidePerfect)
      return HitMarginHidePreset.HideXPerfect;
    if (!options.HideEarlyPerfect || !options.HideLatePerfect)
      return HitMarginHidePreset.HidePerfect;
    if (!options.HideEarly || !options.HideLate)
      return HitMarginHidePreset.HideEarlyOrLatePerfect;
    if (!options.HideTooEarly || !options.HideTooLate || !options.HideMiss || !options.HideOverload)
      return HitMarginHidePreset.HideCounted;
    return HitMarginHidePreset.HideAll;
  }

  /// <summary>Hides exactly the judgments <paramref name="step"/> hides and shows the rest.</summary>
  public static void Apply(JudgementVisualOptions options, HitMarginHidePreset step)
  {
    options.HideXPerfect = step >= HitMarginHidePreset.HideXPerfect;
    options.HidePerfect = step >= HitMarginHidePreset.HidePerfect;
    options.HideEarlyPerfect = options.HideLatePerfect = step >= HitMarginHidePreset.HideEarlyOrLatePerfect;
    options.HideEarly = options.HideLate = step >= HitMarginHidePreset.HideCounted;
    options.HideTooEarly =
      options.HideTooLate =
      options.HideMiss =
      options.HideOverload =
        step >= HitMarginHidePreset.HideAll;
  }
}
