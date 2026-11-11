using System;
using System.Collections.Generic;
using Element.Configuration;
using Element.Domain.Judgement;
using Element.Domain.Overlay.Values;

namespace Element.Domain.Overlay.Defaults;

/// <summary>
/// The runs of the built-in texts, which also stand in for the values texts
/// saved by 1.1.0 used before they were split up.
/// </summary>
internal static class OverlayDefaultRuns
{
  // The judgment counts in the order the built-in text shows them.
  private static readonly OverlayValueId[] JudgementRow =
  {
    OverlayValueId.Overload,
    OverlayValueId.TooEarly,
    OverlayValueId.VeryEarly,
    OverlayValueId.EarlyPerfect,
    OverlayValueId.PerfectEarly,
    OverlayValueId.PurePerfect,
    OverlayValueId.PerfectLate,
    OverlayValueId.LatePerfect,
    OverlayValueId.VeryLate,
    OverlayValueId.TooLate,
    OverlayValueId.Miss,
  };

  /// <summary>"0%-48%": start and current progress with their percent signs.</summary>
  public static List<OverlayTextRun> Progress(OverlayColorSettings color, float size) =>
    new()
    {
      Run("progress-start", color, size),
      Literal("%-", color, size),
      Run("progress-current", color, size),
      Literal("%", color, size),
    };

  /// <summary>"1:27~3:48": elapsed and total time.</summary>
  public static List<OverlayTextRun> Time(OverlayColorSettings color, float size) =>
    new() { Run("time-current", color, size), Literal("~", color, size), Run("time-total", color, size) };

  public static Func<OverlayColorSettings, float, List<OverlayTextRun>> Percent(string key) =>
    (color, size) => new() { Run(key, color, size), Literal("%", color, size) };

  /// <summary>The colored row of every judgment count; <paramref name="color"/> is its base color.</summary>
  public static List<OverlayTextRun> Judgements(OverlayColorSettings color, float size)
  {
    List<OverlayTextRun> runs = new();
    for (int i = 0; i < JudgementRow.Length; i++)
    {
      if (i > 0)
        runs.Add(Literal(" ", color, size));
      OverlayValueId id = JudgementRow[i];
      runs.Add(
        Run(
          OverlayValueCatalog.Get(id).Key,
          JudgementColors.TryGet(id, out string hex) ? OverlayColorSettings.Custom(hex, color.Alpha) : color.Clone(),
          size
        )
      );
    }
    return runs;
  }

  public static OverlayTextRun Run(string key, OverlayColorSettings color, float size)
  {
    OverlayTextRun run = OverlayTextRun.ValueRun(key, color.Clone());
    run.Size = size;
    return run;
  }

  public static OverlayTextRun Literal(string text, OverlayColorSettings color, float size)
  {
    OverlayTextRun run = OverlayTextRun.Literal(text, color.Clone());
    run.Size = size;
    return run;
  }
}
