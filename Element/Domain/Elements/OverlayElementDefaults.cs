using System;
using System.Collections.Generic;
using Element.Configuration;

namespace Element.Domain.Elements;

internal sealed class OverlayDefaultElement
{
  public readonly string Id;
  public readonly string TemplateId;

  // The built-in overlay's font size over the text element's base size.
  public readonly float BaseScale;

  // The point a built-in shape keeps when its size follows a value.
  public readonly OverlayGrowth GrowFrom;
  private readonly Func<List<OverlayTextRun>> _runs;
  private readonly Func<OverlayShapeSettings> _shape;

  public OverlayDefaultElement(string id, float baseScale, Func<List<OverlayTextRun>> runs)
  {
    Id = id;
    TemplateId = OverlayElementKinds.Text;
    BaseScale = baseScale;
    GrowFrom = OverlayGrowth.Auto;
    _runs = runs;
  }

  public OverlayDefaultElement(string id, OverlayGrowth growFrom, Func<OverlayShapeSettings> shape)
  {
    Id = id;
    TemplateId = OverlayElementKinds.Shape;
    BaseScale = 1;
    GrowFrom = growFrom;
    _shape = shape;
  }

  public bool IsShape => _shape != null;

  public List<OverlayTextRun> CreateRuns() => _runs?.Invoke() ?? new List<OverlayTextRun>();

  public OverlayShapeSettings CreateShape() => _shape?.Invoke();
}

// The overlays that used to be fixed presets, now ordinary text elements with
// values. Ids are the old preset ids so saved placements carry over.
internal static class OverlayElementDefaults
{
  public const float TextSize = 25f;
  public const string ComboId = "combo";
  public const string ComboTitleId = "combo-title";
  public const string ProgressBarId = "progress-bar";
  public const string ProgressTrackId = "progress-bar-track";
  public const string ProgressFillId = "progress-bar-fill";
  private const string White = "#FFFFFF";

  // JRP's progress bar as it shows, in 1080p pixels: a black bar along the
  // top, a light gray track 2 px inside it, and a fill in the highlight color
  // over the track as the level goes on, all with 3 px corners. JRP's sprite
  // leaves a clear pixel around each piece, so these are a pixel smaller on
  // every side than its prefab's 642 x 18 and 638 x 14.
  public const float ProgressBarTop = 11,
    ProgressBarWidth = 640,
    ProgressBarHeight = 16,
    ProgressBarEdge = 2,
    ProgressBarRadius = 3;
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
    // In drawing order: the black bar, the track over it, the fill on top.
    new(ProgressBarId, OverlayGrowth.Auto, ProgressEdge),
    new(ProgressTrackId, OverlayGrowth.Auto, ProgressTrack),
    new(ProgressFillId, OverlayGrowth.Left, ProgressFill),
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

  public static OverlayLayoutSettings Create(OverlayDefaultElement element)
  {
    OverlayLayoutSettings settings = new()
    {
      Id = element.Id,
      TemplateId = element.TemplateId,
      Enabled = true,
      DefaultPlacement = element.Id,
      GrowFrom = element.GrowFrom,
    };
    Reset(settings, element);
    return settings;
  }

  /// <summary>
  /// Adds the built-ins a layout doesn't have yet, such as ones added in an
  /// update, at the end. A shape goes before the next built-in shape already
  /// there, so the progress bar's pieces keep drawing in their order.
  /// </summary>
  public static void AddMissing(List<OverlayLayoutSettings> layouts)
  {
    if (layouts == null)
      return;
    for (int i = 0; i < Elements.Length; i++)
    {
      if (IndexOf(layouts, Elements[i].Id) >= 0)
        continue;
      int before = -1;
      for (int next = i + 1; Elements[i].IsShape && next < Elements.Length && before < 0; next++)
        if (Elements[next].IsShape)
          before = IndexOf(layouts, Elements[next].Id);
      OverlayLayoutSettings created = Create(Elements[i]);
      if (before >= 0)
        layouts.Insert(before, created);
      else
        layouts.Add(created);
    }
  }

  public static int IndexOf(List<OverlayLayoutSettings> layouts, string id) =>
    layouts.FindIndex(layout => layout != null && layout.Id == id);

  // The built-in content: a text's runs or a shape's look.
  private static void Reset(OverlayLayoutSettings settings, OverlayDefaultElement element)
  {
    if (element.IsShape)
      settings.Shape = element.CreateShape();
    else
      settings.Text = new OverlayTextSettings { Runs = element.CreateRuns() };
  }

  private static float Inside => ProgressBarWidth - ProgressBarEdge * 2;

  internal static OverlayShapeSettings ProgressEdge()
  {
    OverlayShapeSettings shape = Bar(ProgressBarWidth, ProgressBarHeight);
    shape.Fill = Custom("#000000");
    return shape;
  }

  // JRP's white sprite is this light gray where it isn't lit.
  private static OverlayShapeSettings ProgressTrack()
  {
    OverlayShapeSettings shape = Bar(Inside, ProgressBarHeight - ProgressBarEdge * 2);
    shape.Fill = Custom("#F5F5F5");
    return shape;
  }

  // As wide as the track times the progress; nothing before play.
  internal static OverlayShapeSettings ProgressFill()
  {
    OverlayShapeSettings shape = Bar(OverlayShapeGeometry.MinimumSize, ProgressBarHeight - ProgressBarEdge * 2);
    shape.WidthFormula =
      "{progress-current}*" + (Inside / 100).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
    shape.Fill = OverlayColorSettings.HighlightColor();
    return shape;
  }

  private static OverlayShapeSettings Bar(float width, float height)
  {
    OverlayShapeSettings shape = OverlayShapeGeometry.Create(OverlayShapeKind.Square);
    shape.Width = width;
    shape.Height = height;
    shape.StrokeWidth = 0;
    foreach (OverlayShapePoint point in shape.Points)
      point.Radius = ProgressBarRadius;
    return shape;
  }

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
      if (TryGet(layout.Id, out OverlayDefaultElement element) && layout.TemplateId == element.TemplateId)
      {
        layout.Enabled = true;
        layout.DefaultPlacement = element.Id;
        layout.GrowFrom = element.GrowFrom;
        Reset(layout, element);
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
        || element.IsShape
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
    OverlayElementDefaults.AddMissing(layouts);
  }

  /// <summary>
  /// The first progress bar, one bordered shape of JRP's prefab size, becomes
  /// JRP's three pieces as they show. Each piece still as it came is
  /// replaced; a changed one is left as it is. The new track follows whether
  /// the bar is shown.
  /// </summary>
  public static void UpgradeProgressBar(List<OverlayLayoutSettings> layouts)
  {
    if (layouts == null)
      return;
    int edgeIndex = OverlayElementDefaults.IndexOf(layouts, OverlayElementDefaults.ProgressBarId);
    if (edgeIndex < 0)
      return;
    OverlayLayoutSettings edge = layouts[edgeIndex];
    OverlayShapeSettings old = edge.Shape;
    if (
      old != null
      && old.StrokeWidth == 2
      && old.Width == 642
      && old.Height == 18
      && string.IsNullOrEmpty(old.WidthFormula)
    )
      edge.Shape = OverlayElementDefaults.ProgressEdge();
    int fillIndex = OverlayElementDefaults.IndexOf(layouts, OverlayElementDefaults.ProgressFillId);
    OverlayShapeSettings fill = fillIndex >= 0 ? layouts[fillIndex].Shape : null;
    if (fill != null && fill.Height == 14 && fill.WidthFormula == "{progress-current}*6.38")
    {
      OverlayColorSettings color = fill.Fill;
      layouts[fillIndex].Shape = OverlayElementDefaults.ProgressFill();
      // A color picked by hand stays; the first purple becomes the highlight.
      if (color != null && !color.Highlight && !string.Equals(color.Hex, "#EBCDF9", StringComparison.OrdinalIgnoreCase))
        layouts[fillIndex].Shape.Fill = color;
    }
    if (OverlayElementDefaults.IndexOf(layouts, OverlayElementDefaults.ProgressTrackId) >= 0)
      return;
    OverlayElementDefaults.TryGet(OverlayElementDefaults.ProgressTrackId, out OverlayDefaultElement track);
    OverlayLayoutSettings created = OverlayElementDefaults.Create(track);
    created.Enabled = edge.Enabled;
    layouts.Insert(edgeIndex + 1, created);
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
