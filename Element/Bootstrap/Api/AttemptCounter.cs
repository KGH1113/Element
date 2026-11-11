using System;
using Element.Application.Attempts;

namespace Element;

/// <summary>
/// Lets another mod keep plays out of the attempt counter, for example while it
/// plays back a replay. A mod sets one rule, usually when it loads, and the
/// counter asks it each time a play starts. Nothing is switched off and back on,
/// so a forgotten call or a crash in the middle cannot leave counting off.
/// </summary>
/// <remarks>
/// Bind by reflection: the type <c>Element.AttemptCounter</c> in the
/// assembly named <c>Element</c>, after checking <see cref="ApiVersion"/>.
/// Rules stay set while this mod is turned off and on in UnityModManager.
/// See docs/attempt-counter-api.md.
/// </remarks>
public static class AttemptCounter
{
  /// <summary>Changes only when these methods change in a way older callers would break on.</summary>
  public const int ApiVersion = 1;

  internal static readonly AttemptSkipRules Rules = new();

  /// <summary>
  /// Sets <paramref name="owner"/>'s rule, replacing the one it set before; setting
  /// it again is harmless. When a play starts, the counter calls
  /// <paramref name="skip"/> on the main thread, and <c>true</c> keeps that play
  /// out of the count. The overlay still shows the counts so far. A rule that
  /// throws is removed and that play counts.
  /// </summary>
  /// <param name="owner">The mod that sets the rule, such as <c>"TUFReplay"</c>.</param>
  /// <param name="skip">Reads the mod's own state, such as whether a replay is playing.</param>
  public static void SetSkipRule(string owner, Func<bool> skip) => Rules.Set(owner, skip);

  /// <summary>Removes <paramref name="owner"/>'s rule. Nothing happens if it has none.</summary>
  public static void RemoveSkipRule(string owner) => Rules.Remove(owner);
}
