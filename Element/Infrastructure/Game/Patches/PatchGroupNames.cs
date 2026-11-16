namespace Element.Infrastructure.Game.Patches;

/// <summary>
/// The features patches belong to. A game update that breaks one patch turns off
/// only its group. Groups are applied in ordinal order of these names, which sets
/// the order of same-priority patches on one game method.
/// </summary>
internal static class PatchGroupNames
{
  public const string Bpm = "Bpm";
  public const string ChatterBlocker = "ChatterBlocker";

  /// <summary>Each editor tweak hook is its own group: a missing method turns off only that hook.</summary>
  public const string EditorTweaks = "EditorTweaks.";
  public const string Effects = "Effects";
  public const string Input = "Input";
  public const string Judgement = "Judgement";

  // Its own group: if a game update breaks this hook, judgment counting keeps working.
  public const string JudgmentTextSync = "JudgmentTextSync";
  public const string KeyLimiter = "KeyLimiter";
  public const string Lifecycle = "Lifecycle";

  /// <summary>Each planet appearance hook is its own group, like the editor tweaks.</summary>
  public const string PlanetAppearance = "PlanetAppearance.";
  public const string Recording = "Recording";
  public const string Status = "Status";
  public const string Trackpad = "Trackpad";
}
