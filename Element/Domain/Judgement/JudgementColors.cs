using Element.Domain.Overlay.Values;

namespace Element.Domain.Judgement;

/// <summary>
/// The colors judgment counts show in, in the built-in judgment text and when
/// a count is added to a text. Early and late share one color.
/// </summary>
internal static class JudgementColors
{
  public const string TooEarlyOrLate = "#FF0000";
  public const string VeryEarlyOrLate = "#FF6F4E";
  public const string EarlyOrLatePerfect = "#A0FF4E";
  public const string Perfect = "#60FF4E";

  /// <summary>The built-in judgment text's own color; Overload and Miss show in it.</summary>
  public const string Base = "#D958FF";

  /// <summary>
  /// A judgment's own color. False for Overload and Miss, which take the
  /// text's color, and for values that are not judgments.
  /// </summary>
  public static bool TryGet(OverlayValueId id, out string hex)
  {
    hex = id switch
    {
      OverlayValueId.TooEarly or OverlayValueId.TooLate => TooEarlyOrLate,
      OverlayValueId.VeryEarly or OverlayValueId.VeryLate => VeryEarlyOrLate,
      OverlayValueId.EarlyPerfect or OverlayValueId.LatePerfect => EarlyOrLatePerfect,
      OverlayValueId.PerfectEarly or OverlayValueId.PurePerfect or OverlayValueId.PerfectLate => Perfect,
      _ => null,
    };
    return hex != null;
  }
}
