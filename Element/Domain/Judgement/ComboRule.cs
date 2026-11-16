namespace Element.Domain.Judgement;

/// <summary>How a hit changes the combo.</summary>
internal static class ComboRule
{
  /// <summary>Whether <paramref name="hit"/> changes the combo at all; a midspin doesn't.</summary>
  public static bool Affects(HitMargin hit) => HitMarginPolicy.AffectsCombo(hit);

  /// <summary>The combo after <paramref name="hit"/>: one more, or zero when the hit breaks it.</summary>
  public static int After(int count, HitMargin hit)
  {
    if (!Affects(hit))
      return count;
    if (!HitMarginPolicy.ContinuesCombo(hit))
      return 0;
    return count < int.MaxValue ? count + 1 : count;
  }
}
