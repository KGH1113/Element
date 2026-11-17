using System;
using ElementUI;

namespace Element.Presentation.OverlayEditor.Parts;

/// <summary>
/// What a full-screen editor hands down to every view of a part it shows,
/// instead of passing it through each view's constructor. Set where the part
/// is put on screen (<c>.Environment(key, value)</c>); read in a view with
/// <c>EnvironmentValue(key)</c> once it is mounted.
/// </summary>
internal static class EditorEnvironment
{
  /// <summary>How wide words are at the editors' 13-unit size, for lists sized to their longest name.</summary>
  public static readonly EnvironmentKey<Func<string, float>> Measure = new(_ => 0);

  /// <summary>The player asked for less motion: lists stop at once instead of gliding.</summary>
  public static readonly EnvironmentKey<bool> ReduceMotion = new(false);
}
