namespace Element.Configuration.Gameplay;

[CopiedInPlace]
public sealed class JudgementVisualOptions
{
  /// <summary>1 once X-Perfect has its own switch; SettingsMigrator splits it from Perfect.</summary>
  public int Version;
  public bool HidePerfect = true;
  public bool HideXPerfect = true;
  public bool HideEarlyPerfect;
  public bool HideLatePerfect;
  public bool HideTooEarly;
  public bool HideEarly;
  public bool HideLate;
  public bool HideTooLate;
  public bool HideMiss;
  public bool HideOverload;
}
