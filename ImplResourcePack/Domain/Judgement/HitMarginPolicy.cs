namespace ImplResourcePack.Domain.Judgement;

internal static class HitMarginPolicy
{
  public static bool IsPerfect(HitMargin hit) =>
    hit == HitMargin.PerfectMinus || hit == HitMargin.XPerfect || hit == HitMargin.PerfectPlus;

  public static bool ContinuesCombo(HitMargin hit) => IsPerfect(hit) || hit == HitMargin.Auto;

  // The game hits midspin tiles by itself and shows no judgment for them.
  public static bool AffectsCombo(HitMargin hit) => hit != HitMargin.Midspin;
}
