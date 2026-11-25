using System;
using System.Collections.Generic;
using Element.Configuration.Overlay;
using Element.Domain.Overlay.Formula;
using Element.Domain.Overlay.Values;

namespace Element.Domain.Overlay.Defaults;

// Settings saved before 1.1 list fixed presets. Turn each into the equivalent
// text element in place, keeping whether it is shown and where it sits.
// Texts saved by 1.1.0 use values that were later split up or lost their unit;
// MigrateFormulas rewrites them so they still read the same. Colors that
// followed a value become Ifs in ConvertValueColors.
internal static class OverlayElementMigration
{
  // Values that used to end in "%".
  private static readonly HashSet<string> PercentValues = new()
  {
    "progress-start",
    "progress-current",
    "accuracy",
    "timing-scale",
  };

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
            rewritten.AddRange(OverlayDefaultRuns.Progress(color, run.Size));
            break;
          case "time":
            rewritten.AddRange(OverlayDefaultRuns.Time(color, run.Size));
            break;
          case "judgements":
            rewritten.AddRange(OverlayDefaultRuns.Judgements(color, run.Size));
            break;
          case string key when PercentValues.Contains(key):
            rewritten.Add(run);
            rewritten.Add(OverlayDefaultRuns.Literal("%", color, run.Size));
            break;
          default:
            rewritten.Add(run);
            break;
        }
      }
      layout.Text.Runs = rewritten;
    }
  }

  /// <summary>
  /// Before animation could be turned on per value, every combo grew on each
  /// hit, even inside a calculation. Those values keep doing so; nothing else
  /// starts moving.
  /// </summary>
  public static void AnimateCombos(List<OverlayLayoutSettings> layouts)
  {
    if (layouts == null)
      return;
    foreach (OverlayLayoutSettings layout in layouts)
    {
      List<OverlayTextRun> runs = layout?.Text?.Runs;
      if (runs == null)
        continue;
      foreach (OverlayTextRun run in runs)
        if (run != null && run.IsValue && OverlayFormula.Parse(run.Value).Mask.Contains(OverlayValueId.Combo))
          run.Animate = true;
    }
  }

  /// <summary>
  /// A color that followed a value becomes an If. Neighbouring runs that
  /// followed it the same way become one If with a case per stop, read from
  /// the highest: "the value is this stop's number or more" shows the runs in
  /// that stop's color, and otherwise they show in the lowest stop's. A color
  /// that blended between its stops now changes at each stop instead.
  /// </summary>
  public static void ConvertValueColors(List<OverlayLayoutSettings> layouts)
  {
    if (layouts == null)
      return;
    foreach (OverlayLayoutSettings layout in layouts)
    {
      List<OverlayTextRun> runs = layout?.Text?.Runs;
      if (runs == null)
        continue;
      List<OverlayTextRun> rewritten = new(runs.Count);
      for (int i = 0; i < runs.Count; )
      {
        OverlayTextRun run = runs[i];
        OverlayValueColor follows = run?.If == null ? run?.Color?.ValueColor : null;
        if (follows == null)
        {
          if (run != null)
            rewritten.Add(run);
          i++;
          continue;
        }
        int end = i + 1;
        while (
          end < runs.Count && runs[end] != null && runs[end].If == null && follows.SameAs(runs[end].Color?.ValueColor)
        )
          end++;
        rewritten.AddRange(WithoutValueColor(runs.GetRange(i, end - i), follows));
        i = end;
      }
      layout.Text.Runs = rewritten;
    }
  }

  // The runs as one If, or as they are (in their own colors) when the color
  // followed no value or had fewer than two stops to change between.
  private static List<OverlayTextRun> WithoutValueColor(List<OverlayTextRun> runs, OverlayValueColor follows)
  {
    List<OverlayValueColorStop> stops = new();
    if (follows.Stops != null && !OverlayFormula.Parse(follows.Value).IsEmpty)
      foreach (OverlayValueColorStop stop in follows.Stops)
      {
        if (stop == null || float.IsNaN(stop.At) || float.IsInfinity(stop.At))
          continue;
        // Kept lowest first; of two at one number, the one made later counted.
        int at = stops.Count;
        while (at > 0 && stops[at - 1].At > stop.At)
          at--;
        if (at > 0 && stops[at - 1].At == stop.At)
          stops[at - 1] = stop;
        else
          stops.Insert(at, stop);
      }
    if (stops.Count < 2)
    {
      foreach (OverlayTextRun run in runs)
        run.Color.ValueColor = null;
      return runs;
    }
    OverlayTextIf converted = new() { Otherwise = Colored(runs, stops[0]) };
    for (int i = stops.Count - 1; i >= 1 && converted.Cases.Count < OverlayTextIf.MaximumCases; i--)
      converted.Cases.Add(
        new OverlayTextCase
        {
          When = new OverlayCondition
          {
            Value = follows.Value,
            Than = OverlayFormula.NumberText(Math.Round((double)stops[i].At, 4)),
            Same = true,
            Bigger = true,
          },
          Runs = Colored(runs, stops[i]),
        }
      );
    return new List<OverlayTextRun> { new() { If = converted } };
  }

  private static List<OverlayTextRun> Colored(List<OverlayTextRun> runs, OverlayValueColorStop stop)
  {
    List<OverlayTextRun> colored = new(runs.Count);
    foreach (OverlayTextRun run in runs)
    {
      OverlayTextRun copy = run.Clone();
      copy.Color = stop.Highlight
        ? OverlayColorSettings.HighlightColor(stop.Alpha)
        : OverlayColorSettings.Custom(stop.Hex, stop.Alpha);
      colored.Add(copy);
    }
    return colored;
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
