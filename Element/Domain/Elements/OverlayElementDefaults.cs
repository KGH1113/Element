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
  private const string JudgementBase = "#D958FF";

  private static readonly OverlayDefaultElement[] Elements =
  {
    new("progress", 1, () => Labelled("progress-label", "progress")),
    new("accuracy", 1, () => Labelled("accuracy-label", "accuracy")),
    new("time", 1, () => Labelled("time-label", "time")),
    new("fps", 1, () => Prefixed("FPS | ", "fps")),
    new("tile-bpm", 1, () => Prefixed("TBPM | ", "tile-bpm")),
    new("current-bpm", 1, () => Prefixed("CBPM | ", "current-bpm")),
    new("keys-per-second", 1, () => Prefixed("KPS | ", "kps")),
    new("song-title", 24f / TextSize, () => new() { Value("song-title", Custom(White)) }),
    new(ComboId, 78f / TextSize, () => new() { Value("combo", OverlayColorSettings.HighlightColor()) }),
    new(ComboTitleId, 40f / TextSize, () => new() { OverlayTextRun.Literal("Perfect", Custom(White)) }),
    new("judgement", 1, () => new() { Value("judgements", Custom(JudgementBase)) }),
    new("timing-scale", 20f / TextSize, () => Plain("Timing Scale - ", "timing-scale")),
    new("attempt", 1, () => Plain("Attempt ", "attempt")),
    new("full-attempt", 1, () => Plain("Full Attempt ", "full-attempt")),
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

  private static List<OverlayTextRun> Labelled(string label, string value) =>
    new()
    {
      Value(label, Custom(White)),
      OverlayTextRun.Literal(" | ", Custom(White)),
      Value(value, OverlayColorSettings.HighlightColor()),
    };

  private static List<OverlayTextRun> Prefixed(string prefix, string value) =>
    new() { OverlayTextRun.Literal(prefix, Custom(White)), Value(value, OverlayColorSettings.HighlightColor()) };

  private static List<OverlayTextRun> Plain(string prefix, string value) =>
    new() { OverlayTextRun.Literal(prefix, Custom(White)), Value(value, Custom(White)) };

  private static OverlayTextRun Value(string key, OverlayColorSettings color) => OverlayTextRun.ValueRun(key, color);

  private static OverlayColorSettings Custom(string hex) => OverlayColorSettings.Custom(hex);
}

// Settings saved before 1.1 list fixed presets. Turn each into the equivalent
// text element in place, keeping whether it is shown and where it sits.
internal static class OverlayElementMigration
{
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
