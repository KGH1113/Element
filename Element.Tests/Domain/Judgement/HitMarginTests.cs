using Element.Domain.Judgement;
using Element.Tests.Support;
using static Element.Tests.Support.Assert;

namespace Element.Tests.Domain.Judgement;

/// <summary>X-Perfect hits: shown as perfect, kept in the combo, counted on their own.</summary>
internal static class HitMarginTests
{
  [Suite("classifies X-Perfect hit margins")]
  public static void ClassifiesXPerfectHitMargins()
  {
    foreach (HitMargin hit in new[] { HitMargin.PerfectMinus, HitMargin.XPerfect, HitMargin.PerfectPlus })
    {
      Check(HitMarginPolicy.IsPerfect(hit), hit + " should be a perfect judgment");
      Check(HitMarginPolicy.ContinuesCombo(hit), hit + " should continue the combo");
    }

    Check(!HitMarginPolicy.IsPerfect(HitMargin.Auto), "Auto should not be shown as a perfect judgment");
    Check(HitMarginPolicy.ContinuesCombo(HitMargin.Auto), "Auto should continue the combo");
    Check(!HitMarginPolicy.IsPerfect(HitMargin.Multipress), "Multipress should remain distinct");
    Check(!HitMarginPolicy.ContinuesCombo(HitMargin.EarlyPerfect), "EarlyPerfect should reset the combo");
  }

  [Suite("represents X-Perfect counts separately")]
  public static void RepresentsXPerfectCountsSeparately()
  {
    JudgementSnapshot snapshot = new(1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11);

    Equal(5, snapshot.PerfectMinus);
    Equal(6, snapshot.XPerfectAndAuto);
    Equal(7, snapshot.PerfectPlus);
  }
}
