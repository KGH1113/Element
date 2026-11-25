using System;
using System.Collections.Generic;
using Element.Configuration.Overlay;
using Element.Domain.Judgement;
using Element.Domain.Layout;
using Element.Domain.Overlay.Shape;

namespace Element.Domain.Overlay.Defaults;

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

  // The groups the built-ins start in; a built-in group has no typed name.
  public const string ProgressBarGroup = "progress-bar";
  public const string ComboGroup = "combo";
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
  internal const string JudgementBase = JudgementColors.Base;

  private static readonly OverlayDefaultElement[] Elements =
  {
    new("progress", 1, () => Labelled("progress-label", Highlighted(OverlayDefaultRuns.Progress))),
    new("accuracy", 1, () => Labelled("accuracy-label", Highlighted(OverlayDefaultRuns.Percent("accuracy")))),
    new("time", 1, () => Labelled("time-label", Highlighted(OverlayDefaultRuns.Time))),
    new("fps", 1, () => Prefixed("FPS | ", "fps")),
    new("tile-bpm", 1, () => Prefixed("TBPM | ", "tile-bpm")),
    new("current-bpm", 1, () => Prefixed("CBPM | ", "current-bpm")),
    new("keys-per-second", 1, () => Prefixed("KPS | ", "kps")),
    new("song-title", 24f / TextSize, () => new() { Value("song-title", Custom(White)) }),
    new(ComboId, 78f / TextSize, () => new() { Animated(Value("combo", OverlayColorSettings.HighlightColor())) }),
    new(ComboTitleId, 40f / TextSize, () => new() { OverlayTextRun.Literal("Perfect", Custom(White)) }),
    new("judgement", 1, () => OverlayDefaultRuns.Judgements(Custom(JudgementColors.Base), 0)),
    new("timing-scale", 20f / TextSize, () => Plain("Timing Scale - ", OverlayDefaultRuns.Percent("timing-scale"))),
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
      Group = GroupOf(element.Id),
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

  /// <summary>The group a built-in starts in: the progress bar's pieces, the combo and its title.</summary>
  public static string GroupOf(string id) =>
    id switch
    {
      ProgressBarId or ProgressTrackId or ProgressFillId => ProgressBarGroup,
      ComboId or ComboTitleId => ComboGroup,
      _ => string.Empty,
    };

  /// <summary>
  /// Puts the built-ins in their groups, unless one was put in another group
  /// by hand. A group left with one shown element is dropped again.
  /// </summary>
  public static void GroupBuiltIns(List<OverlayLayoutSettings> layouts)
  {
    if (layouts == null)
      return;
    foreach (OverlayLayoutSettings layout in layouts)
    {
      if (
        layout == null
        || !TryGet(layout.Id, out OverlayDefaultElement element)
        || layout.TemplateId != element.TemplateId
      )
        continue;
      string group = GroupOf(layout.Id);
      if (group.Length == 0 || (!string.IsNullOrEmpty(layout.Group) && layout.Group != group))
        continue;
      layout.Group = group;
      layout.GroupName = string.Empty;
    }
    LayoutGroups.KeepTogether(layouts);
    LayoutGroups.DropLonely(layouts);
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
    // The built-ins draw in their first order again, in their groups.
    LayoutLayers.SortByLegacyLayer(layouts);
    foreach (OverlayLayoutSettings layout in layouts)
      if (
        layout != null
        && TryGet(layout.Id, out OverlayDefaultElement element)
        && layout.TemplateId == element.TemplateId
      )
      {
        layout.Group = GroupOf(layout.Id);
        layout.GroupName = string.Empty;
      }
    LayoutGroups.KeepTogether(layouts);
    LayoutGroups.DropLonely(layouts);
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
    (color, size) => new() { OverlayDefaultRuns.Run(key, color, size) };

  private static OverlayTextRun Value(string key, OverlayColorSettings color) => OverlayTextRun.ValueRun(key, color);

  // The combo grows on every hit, as it always has.
  private static OverlayTextRun Animated(OverlayTextRun run)
  {
    run.Animate = true;
    return run;
  }

  private static OverlayColorSettings Custom(string hex) => OverlayColorSettings.Custom(hex);
}
