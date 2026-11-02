using System;
using System.Collections.Generic;
using Element.Configuration;

namespace Element.Domain.Elements;

internal sealed class OverlayDefaultElement
{
  public readonly string Id;

  // The built-in overlay's font size over the text element's base size.
  public readonly float BaseScale;
  private readonly Func<List<OverlayTextRun>> _runs;

  public OverlayDefaultElement(string id, float baseScale, Func<List<OverlayTextRun>> runs)
  {
    Id = id;
    BaseScale = baseScale;
    _runs = runs;
  }

  public List<OverlayTextRun> CreateRuns() => _runs();
}

// The overlays that used to be fixed presets, now ordinary text elements with
// values. Ids are the old preset ids so saved placements carry over.
internal static class OverlayElementDefaults
{
  public const float TextSize = 25f;
  public const string ComboId = "combo";
  public const string ComboTitleId = "combo-title";
  private const string White = "#FFFFFF";
  internal const string JudgementBase = "#D958FF";

  private static readonly OverlayDefaultElement[] Elements =
  {
    new("progress", 1, () => Labelled("progress-label", Highlighted(OverlayElementMigration.Progress))),
    new("accuracy", 1, () => Labelled("accuracy-label", Highlighted(OverlayElementMigration.Percent("accuracy")))),
    new("time", 1, () => Labelled("time-label", Highlighted(OverlayElementMigration.Time))),
    new("fps", 1, () => Prefixed("FPS | ", "fps")),
    new("tile-bpm", 1, () => Prefixed("TBPM | ", "tile-bpm")),
    new("current-bpm", 1, () => Prefixed("CBPM | ", "current-bpm")),
    new("keys-per-second", 1, () => Prefixed("KPS | ", "kps")),
    new("song-title", 24f / TextSize, () => new() { Value("song-title", Custom(White)) }),
    new(ComboId, 78f / TextSize, () => new() { Value("combo", OverlayColorSettings.HighlightColor()) }),
    new(ComboTitleId, 40f / TextSize, () => new() { OverlayTextRun.Literal("Perfect", Custom(White)) }),
    new("judgement", 1, () => OverlayElementMigration.Judgements(Custom(JudgementBase), 0)),
    new(
      "timing-scale",
      20f / TextSize,
      () => Plain("Timing Scale - ", OverlayElementMigration.Percent("timing-scale"))
    ),
    new("attempt", 1, () => Plain("Attempt ", Single("attempt"))),
    new("full-attempt", 1, () => Plain("Full Attempt ", Single("full-attempt"))),
  };

  public static IReadOnlyList<OverlayDefaultElement> All => Elements;

  public static bool TryGet(string id, out OverlayDefaultElement element)
  {
    foreach (OverlayDefaultElement candidate in Elements)
      if (candidate.Id == id)
      {
        element = candidate;
        return true;
      }
    element = null;
    return false;
  }

  public static OverlayLayoutSettings Create(OverlayDefaultElement element) =>
    new()
    {
      Id = element.Id,
      TemplateId = OverlayElementKinds.Text,
      Enabled = true,
      DefaultPlacement = element.Id,
      Text = new OverlayTextSettings { Runs = element.CreateRuns() },
    };

  /// <summary>
  /// Reset all: built-in texts return with their original content and place,
  /// the given other defaults return to their place, everything else is hidden.
  /// Hidden entries stay so undo can bring back the same elements.
  /// </summary>
  public static void Restore(List<OverlayLayoutSettings> layouts, ICollection<string> otherDefaults)
  {
    HashSet<string> present = new();
    foreach (OverlayLayoutSettings layout in layouts)
    {
      if (layout == null)
        continue;
      present.Add(layout.Id);
      ResetPlacement(layout);
      if (TryGet(layout.Id, out OverlayDefaultElement element) && layout.TemplateId == OverlayElementKinds.Text)
      {
        layout.Enabled = true;
        layout.DefaultPlacement = element.Id;
        layout.Text = new OverlayTextSettings { Runs = element.CreateRuns() };
      }
      else
        layout.Enabled = otherDefaults != null && otherDefaults.Contains(layout.Id);
    }
    foreach (OverlayDefaultElement element in Elements)
      if (!present.Contains(element.Id))
        layouts.Add(Create(element));
  }

  public static void ResetPlacement(OverlayLayoutSettings layout)
  {
    layout.Customized = false;
    layout.LayoutVersion = 0;
    layout.Anchor = Anchor9.Center;
    layout.OffsetX = layout.OffsetY = 0;
    layout.CenterX = layout.CenterY = .5f;
    layout.Scale = 1;
    layout.PendingGroup = string.Empty;
  }

  private static List<OverlayTextRun> Labelled(string label, List<OverlayTextRun> value)
  {
    List<OverlayTextRun> runs = new() { Value(label, Custom(White)), OverlayTextRun.Literal(" | ", Custom(White)) };
    runs.AddRange(value);
    return runs;
  }

  private static List<OverlayTextRun> Prefixed(string prefix, string value) =>
    new() { OverlayTextRun.Literal(prefix, Custom(White)), Value(value, OverlayColorSettings.HighlightColor()) };

  private static List<OverlayTextRun> Plain(
    string prefix,
    Func<OverlayColorSettings, float, List<OverlayTextRun>> value
  )
  {
    List<OverlayTextRun> runs = new() { OverlayTextRun.Literal(prefix, Custom(White)) };
    runs.AddRange(value(Custom(White), 0));
    return runs;
  }

  private static List<OverlayTextRun> Highlighted(Func<OverlayColorSettings, float, List<OverlayTextRun>> value) =>
    value(OverlayColorSettings.HighlightColor(), 0);

  private static Func<OverlayColorSettings, float, List<OverlayTextRun>> Single(string key) =>
    (color, size) => new() { OverlayElementMigration.Run(key, color, size) };

  private static OverlayTextRun Value(string key, OverlayColorSettings color) => OverlayTextRun.ValueRun(key, color);

  private static OverlayColorSettings Custom(string hex) => OverlayColorSettings.Custom(hex);
}

// Settings saved before 1.1 list fixed presets. Turn each into the equivalent
// text element in place, keeping whether it is shown and where it sits.
// Texts saved by 1.1.0 use values that were later split up or lost their unit;
// MigrateFormulas rewrites them so they still read the same.
internal static class OverlayElementMigration
{
  private static readonly (string Key, string Color)[] JudgementRow =
  {
    ("overload", null),
    ("too-early", "#FF0000"),
    ("very-early", "#FF6F4E"),
    ("early-perfect", "#A0FF4E"),
    ("perfect-early", "#60FF4E"),
    ("pure-perfect", "#60FF4E"),
    ("perfect-late", "#60FF4E"),
    ("late-perfect", "#A0FF4E"),
    ("very-late", "#FF6F4E"),
    ("too-late", "#FF0000"),
    ("miss", null),
  };

  // Values that used to end in "%".
  private static readonly HashSet<string> PercentValues = new()
  {
    "progress-start",
    "progress-current",
    "accuracy",
    "timing-scale",
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
      (string key, string hex) = JudgementRow[i];
      runs.Add(Run(key, hex == null ? color.Clone() : OverlayColorSettings.Custom(hex, color.Alpha), size));
    }
    return runs;
  }

  public static OverlayTextRun Run(string key, OverlayColorSettings color, float size)
  {
    OverlayTextRun run = OverlayTextRun.ValueRun(key, color.Clone());
    run.Size = size;
    return run;
  }

  private static OverlayTextRun Literal(string text, OverlayColorSettings color, float size)
  {
    OverlayTextRun run = OverlayTextRun.Literal(text, color.Clone());
    run.Size = size;
    return run;
  }

  /// <summary>Rewrites 1.1.0 texts: split values become their parts, and lost units come back as text.</summary>
  public static void MigrateFormulas(List<OverlayLayoutSettings> layouts)
  {
    if (layouts == null)
      return;
    foreach (OverlayLayoutSettings layout in layouts)
    {
      List<OverlayTextRun> runs = layout?.Text?.Runs;
      if (runs == null)
        continue;
      List<OverlayTextRun> rewritten = new(runs.Count);
      foreach (OverlayTextRun run in runs)
      {
        if (run == null)
          continue;
        OverlayColorSettings color = run.Color ?? OverlayColorSettings.Custom("#FFFFFF");
        switch (run.IsValue ? run.Value : null)
        {
          case "progress":
            rewritten.AddRange(Progress(color, run.Size));
            break;
          case "time":
            rewritten.AddRange(Time(color, run.Size));
            break;
          case "judgements":
            rewritten.AddRange(Judgements(color, run.Size));
            break;
          case string key when PercentValues.Contains(key):
            rewritten.Add(run);
            rewritten.Add(Literal("%", color, run.Size));
            break;
          default:
            rewritten.Add(run);
            break;
        }
      }
      layout.Text.Runs = rewritten;
    }
  }

  public static void Migrate(List<OverlayLayoutSettings> layouts)
  {
    if (layouts == null)
      return;
    for (int i = 0; i < layouts.Count; i++)
    {
      OverlayLayoutSettings layout = layouts[i];
      if (layout == null)
        continue;
      string template = string.IsNullOrEmpty(layout.TemplateId) ? layout.Id : layout.TemplateId;
      if (
        template == OverlayElementDefaults.ComboTitleId
        || !OverlayElementDefaults.TryGet(template, out OverlayDefaultElement element)
      )
        continue;
      bool copy = layout.Id != template;
      layout.TemplateId = OverlayElementKinds.Text;
      layout.Text = new OverlayTextSettings { Runs = element.CreateRuns() };
      // Copies were placed when added, so they never used a built-in position.
      layout.DefaultPlacement = copy ? string.Empty : template;
      layout.PendingGroup = string.Empty;
      if (template == OverlayElementDefaults.ComboId)
      {
        layouts.Insert(++i, ComboTitleFor(layout, copy));
        if (layout.Customized)
          layout.PendingGroup = layout.Id;
        // Both halves are measured in their built-in arrangement first.
        layout.DefaultPlacement = template;
        continue;
      }
      if (layout.Customized)
        layout.Scale *= element.BaseScale;
    }
    foreach (OverlayDefaultElement element in OverlayElementDefaults.All)
      if (layouts.Find(layout => layout != null && layout.Id == element.Id) == null)
        layouts.Add(OverlayElementDefaults.Create(element));
  }

  private static OverlayLayoutSettings ComboTitleFor(OverlayLayoutSettings combo, bool copy)
  {
    OverlayElementDefaults.TryGet(OverlayElementDefaults.ComboTitleId, out OverlayDefaultElement title);
    OverlayLayoutSettings settings = OverlayElementDefaults.Create(title);
    settings.Id = copy ? combo.Id + "/title" : OverlayElementDefaults.ComboTitleId;
    settings.Enabled = combo.Enabled;
    if (combo.Customized)
    {
      settings.CopyPlacementFrom(combo);
      settings.PendingGroup = combo.Id;
    }
    return settings;
  }
}
