using Element.Configuration;

namespace Element.Domain.Judgement;

internal static class JudgementVisualPolicy
{
  // Use the game's enum names so XPerfect stays distinct from the two signed
  // Perfect margins; this policy still needs no Unity objects or native calls.
  public static bool ShouldShow(JudgementVisualOptions settings, bool recording, int margin)
  {
    if (recording)
      return false;
    if (settings == null)
      return true;
    return (HitMargin)margin switch
    {
      HitMargin.TooEarly => !settings.HideTooEarly,
      HitMargin.VeryEarly => !settings.HideEarly,
      HitMargin.EarlyPerfect => !settings.HideEarlyPerfect,
      HitMargin.PerfectMinus or HitMargin.PerfectPlus => !settings.HidePerfect,
      HitMargin.XPerfect => !settings.HideXPerfect,
      HitMargin.LatePerfect => !settings.HideLatePerfect,
      HitMargin.VeryLate => !settings.HideLate,
      HitMargin.TooLate => !settings.HideTooLate,
      HitMargin.FailMiss or HitMargin.FailedFloor => !settings.HideMiss,
      HitMargin.Multipress or HitMargin.FailOverload or HitMargin.OverPress => !settings.HideOverload,
      _ => true,
    };
  }
}
