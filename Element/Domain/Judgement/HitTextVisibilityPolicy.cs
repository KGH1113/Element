namespace Element.Domain.Judgement;

internal static class HitTextVisibilityPolicy
{
  public static bool ShouldShow(bool hidePerfectJudgmentText, bool recordMode, bool isPerfect)
  {
    if (recordMode)
      return false;

    return !hidePerfectJudgmentText || !isPerfect;
  }
}
