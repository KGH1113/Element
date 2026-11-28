using System;
using System.Collections.Generic;
using System.Linq;
using Element.Configuration.Overlay;
using Element.Domain.Overlay.Defaults;
using Element.Domain.Overlay.Shape;
using Element.Domain.Overlay.Text;
using Element.Domain.Overlay.Values;
using static Element.Tests.Domain.Overlay.OverlayFixtures;
using static Element.Tests.Support.Assert;

namespace Element.Tests.Domain.Overlay;

/// <summary>
/// Settings from older versions become today's elements once: presets become
/// texts, the built-in progress bar and combo, and values that were split or lost
/// their unit.
/// </summary>
internal static class OverlayMigrationTests
{
  internal static void Run()
  {
    MigratesBuiltInsToText();
    MigratesFreshInstallToDefaults();
    ProgressBarIsBuiltIn();
    UpgradesTheFirstProgressBar();
    SplitsTheComboIntoTwoTexts();
    LegacyLabelsBecomeWords();
    MigratesSplitValuesAndUnits();
    SettingsMigrateOnceByVersion();
  }

  private static void MigratesBuiltInsToText()
  {
    List<OverlayLayoutSettings> layouts = new()
    {
      new() { Id = "progress" },
      new()
      {
        Id = "timing-scale",
        Enabled = false,
        Customized = true,
        Scale = 2,
        LayoutVersion = 1,
        Anchor = Anchor9.Bottom,
        OffsetX = 12,
        OffsetY = -40,
      },
      new()
      {
        Id = "fps:copy",
        TemplateId = "fps",
        Customized = true,
        Scale = 1.5f,
        LayoutVersion = 1,
        Anchor = Anchor9.TopRight,
      },
      new() { Id = "dmnote-hand", TemplateId = "native-hand" },
      new() { Id = "native-foot:1", TemplateId = "native-foot" },
    };
    OverlayElementMigration.Migrate(layouts);
    OverlayLayoutSettings progress = Find(layouts, "progress");
    Check(progress.TemplateId == OverlayElementKinds.Text && progress.Enabled, "progress becomes a shown text");
    Check(progress.DefaultPlacement == "progress" && !progress.Customized, "progress keeps its built-in place");
    Check(
      Describe(progress.Text.Runs)
        == "[progress-label|custom]' | '[progress-start|highlight]'%-'[progress-current|highlight]'%'",
      "progress reads like before: " + Describe(progress.Text.Runs)
    );
    OverlayLayoutSettings timing = Find(layouts, "timing-scale");
    Check(!timing.Enabled, "a deleted built-in stays deleted");
    Check(
      timing.Customized && timing.Anchor == Anchor9.Bottom && timing.OffsetX == 12 && timing.OffsetY == -40,
      "a moved built-in keeps its anchor and offset"
    );
    Check(Math.Abs(timing.Scale - 2 * 20f / 25f) < 1e-5f, "its scale includes the old 20pt font size");
    OverlayLayoutSettings copy = Find(layouts, "fps:copy");
    Check(copy.TemplateId == OverlayElementKinds.Text && copy.DefaultPlacement == "", "copies become free texts");
    Check(Describe(copy.Text.Runs) == "'FPS | '[fps|highlight]", "copies keep their preset's text");
    Check(Find(layouts, "dmnote-hand").TemplateId == "native-hand", "key viewers are untouched");
    Check(Find(layouts, "native-foot:1").Text == null, "added key viewers are untouched");
    foreach (OverlayDefaultElement element in OverlayElementDefaults.All)
      Check(Find(layouts, element.Id) != null, element.Id + " exists after migration");
    int count = layouts.Count;
    OverlayElementMigration.Migrate(layouts);
    Check(
      layouts.Count == count && Describe(Find(layouts, "fps:copy").Text.Runs) == "'FPS | '[fps|highlight]",
      "migration is stable"
    );
  }

  private static void MigratesFreshInstallToDefaults()
  {
    List<OverlayLayoutSettings> layouts = new();
    OverlayElementMigration.Migrate(layouts);
    Check(
      layouts.Count == OverlayElementDefaults.All.Count
        && layouts.All(layout => layout.Enabled && layout.DefaultPlacement == layout.Id && !layout.Customized),
      "a fresh install starts with every built-in shown in its place"
    );
    Check(
      layouts.Count(layout => layout.TemplateId == OverlayElementKinds.Shape) == 3
        && layouts.All(layout =>
          layout.TemplateId == OverlayElementKinds.Shape ? layout.Shape != null : layout.Text != null
        ),
      "the built-ins are texts and the progress bar's three shapes"
    );
  }

  // JRP's bar as it shows: a black bar, a light gray track 2 px inside it and
  // a fill in the highlight color over the track, growing from its left.
  private static void ProgressBarIsBuiltIn()
  {
    List<OverlayLayoutSettings> layouts = new();
    OverlayElementDefaults.AddMissing(layouts);
    int edge = OverlayElementDefaults.IndexOf(layouts, OverlayElementDefaults.ProgressBarId),
      track = OverlayElementDefaults.IndexOf(layouts, OverlayElementDefaults.ProgressTrackId),
      fill = OverlayElementDefaults.IndexOf(layouts, OverlayElementDefaults.ProgressFillId);
    Check(edge >= 0 && track == edge + 1 && fill == track + 1, "the bar, its track and its fill draw in that order");
    OverlayShapeSettings bar = layouts[edge].Shape,
      inside = layouts[track].Shape,
      progress = layouts[fill].Shape;
    Check(
      bar.Width == 640
        && bar.Height == 16
        && bar.StrokeWidth == 0
        && bar.Fill.Hex == "#000000"
        && inside.Width == 636
        && inside.Height == 12
        && inside.Fill.Hex == "#F5F5F5"
        && bar.Points.All(point => point.Radius == 3)
        && inside.Points.All(point => point.Radius == 3),
      "a black 640 x 16 bar with a light gray 636 x 12 track, both with 3 px corners"
    );
    Check(
      progress.Height == 12
        && progress.Fill.Highlight
        && progress.StrokeWidth == 0
        && progress.WidthFormula == "{progress-current}*6.36"
        && layouts[fill].GrowFrom == OverlayGrowth.Left,
      "the fill follows the highlight color, covers the track at 100% and grows from its left: " + progress.WidthFormula
    );
    Numbers numbers = new() { [OverlayValueId.ProgressCurrent] = 50 };
    Check(Evaluate(progress.WidthFormula, numbers, out _) == 318, "half way fills half the track");

    // Deleting it keeps it gone: missing built-ins arrive once, by version.
    layouts[edge].Enabled = false;
    int count = layouts.Count;
    OverlayElementDefaults.AddMissing(layouts);
    Check(layouts.Count == count && !layouts[edge].Enabled, "a deleted built-in stays deleted");
    OverlayElementDefaults.Restore(layouts, null);
    // Reset all also puts the built-ins back in their drawing order.
    edge = OverlayElementDefaults.IndexOf(layouts, OverlayElementDefaults.ProgressBarId);
    track = OverlayElementDefaults.IndexOf(layouts, OverlayElementDefaults.ProgressTrackId);
    fill = OverlayElementDefaults.IndexOf(layouts, OverlayElementDefaults.ProgressFillId);
    Check(
      layouts[edge].Enabled
        && layouts[track].Enabled
        && layouts[fill].Enabled
        && layouts[fill].Shape.WidthFormula == progress.WidthFormula
        && edge < track
        && track < fill,
      "Reset all brings the progress bar back, its pieces in their order"
    );
  }

  // The first bar was one bordered shape at JRP's prefab size; an untouched
  // one becomes the three pieces, with the track slipped in under the fill.
  private static void UpgradesTheFirstProgressBar()
  {
    OverlayShapeSettings Old(float width, float height, string formula)
    {
      OverlayShapeSettings shape = OverlayShapeGeometry.Create(OverlayShapeKind.Square);
      shape.Width = width;
      shape.Height = height;
      shape.WidthFormula = formula;
      return shape;
    }
    List<OverlayLayoutSettings> layouts = new()
    {
      new() { Id = "song-title", TemplateId = OverlayElementKinds.Text },
      new()
      {
        Id = OverlayElementDefaults.ProgressBarId,
        TemplateId = OverlayElementKinds.Shape,
        Enabled = true,
        Shape = Old(642, 18, ""),
      },
      new()
      {
        Id = OverlayElementDefaults.ProgressFillId,
        TemplateId = OverlayElementKinds.Shape,
        Enabled = true,
        Shape = Old(1, 14, "{progress-current}*6.38"),
      },
    };
    layouts[1].Shape.StrokeWidth = 2;
    layouts[2].Shape.Fill = OverlayColorSettings.Custom("#EBCDF9");
    OverlayElementMigration.UpgradeProgressBar(layouts);
    Check(
      layouts.Count == 4
        && layouts[2].Id == OverlayElementDefaults.ProgressTrackId
        && layouts[2].Enabled
        && layouts[1].Shape.Width == 640
        && layouts[1].Shape.StrokeWidth == 0
        && layouts[3].Shape.Height == 12
        && layouts[3].Shape.Fill.Highlight,
      "an untouched first bar becomes the black bar, the track and the highlight fill"
    );

    List<OverlayLayoutSettings> changed = new()
    {
      new()
      {
        Id = OverlayElementDefaults.ProgressBarId,
        TemplateId = OverlayElementKinds.Shape,
        Enabled = false,
        Shape = Old(500, 30, ""),
      },
      new()
      {
        Id = OverlayElementDefaults.ProgressFillId,
        TemplateId = OverlayElementKinds.Shape,
        Shape = Old(1, 14, "{progress-current}*6.38"),
      },
    };
    changed[1].Shape.Fill = OverlayColorSettings.Custom("#00FF00");
    OverlayElementMigration.UpgradeProgressBar(changed);
    Check(
      changed[0].Shape.Width == 500 && changed[2].Shape.Fill.Hex == "#00FF00" && !changed[1].Enabled,
      "a changed bar and a picked color stay; a deleted bar's new track stays hidden"
    );
  }

  private static void SplitsTheComboIntoTwoTexts()
  {
    List<OverlayLayoutSettings> layouts = new()
    {
      new()
      {
        Id = "combo",
        Customized = true,
        Scale = 1.5f,
        LayoutVersion = 1,
        Anchor = Anchor9.Top,
        OffsetY = 30,
      },
      new() { Id = "combo:copy", TemplateId = "combo" },
    };
    OverlayElementMigration.Migrate(layouts);
    Check(
      layouts[0].Id == "combo" && layouts[1].Id == OverlayElementDefaults.ComboTitleId,
      "the title follows the number"
    );
    Check(Describe(layouts[0].Text.Runs) == "[combo|highlight]", "the number is the combo value");
    Check(Describe(layouts[1].Text.Runs) == "'Perfect'", "the title is plain text");
    Check(layouts[0].PendingGroup == "combo" && layouts[1].PendingGroup == "combo", "a moved combo is fitted as one");
    Check(
      layouts[1].Customized && layouts[1].Scale == 1.5f && layouts[1].OffsetY == 30,
      "the title carries the old placement"
    );
    Check(layouts[0].Scale == 1.5f, "the group fit, not migration, applies the font size");
    Check(
      layouts[2].Id == "combo:copy" && layouts[3].Id == "combo:copy/title" && layouts[2].PendingGroup == "",
      "an unmoved copy gets its own title without a fit"
    );
  }

  private static void LegacyLabelsBecomeWords()
  {
    Check(!OverlayValueCatalog.Categories.Contains(OverlayValueCategory.Legacy), "labels aren't offered as values");
    Check(
      OverlayValueCatalog.Get(OverlayValueId.Accuracy).Category == OverlayValueCategory.Judgement,
      "X-Accuracy sits with judgments"
    );
    Check(
      OverlayValueCatalog.TryGet("progress-start", out OverlayValueDefinition start)
        && OverlayValueCatalog.TryGet("progress-current", out OverlayValueDefinition current)
        && start.Category == OverlayValueCategory.Progress
        && current.Category == OverlayValueCategory.Progress,
      "start and current progress can be added on their own"
    );
    OverlayTextDocument document = OverlayTextDocument.FromRuns(OverlayElementDefaults.All[0].CreateRuns());
    document.ReplaceValues(key => key == "progress-label" ? "진행도" : null);
    Check(
      document.Count == 11 && !document[0].IsValue && document[0].Text == "진" && document[6].Value == "progress-start",
      "a converted label becomes plain words; other values stay"
    );
    Check(
      OverlayValueCatalog.BuiltIn.All(value =>
        value.Key != "progress" && value.Key != "time" && value.Key != "judgements"
      ),
      "values made of other values are gone"
    );
    Check(
      OverlayValueCatalog.BuiltIn.Where(value => value.IsNumeric).All(value => !value.Sample.Contains("%")),
      "values have no units"
    );
  }

  private static void MigratesSplitValuesAndUnits()
  {
    OverlayTextRun Value(string key, float size = 0)
    {
      OverlayTextRun run = OverlayTextRun.ValueRun(key, OverlayColorSettings.Custom("#123456"));
      run.Size = size;
      return run;
    }
    List<OverlayLayoutSettings> layouts = new()
    {
      new()
      {
        Id = "text:1",
        TemplateId = OverlayElementKinds.Text,
        Text = new OverlayTextSettings
        {
          Runs = { Value("progress", 30), Value("time"), Value("accuracy"), Value("combo"), Value("judgements") },
        },
      },
      new()
      {
        Id = "shape:1",
        TemplateId = OverlayElementKinds.Shape,
        Shape = OverlayShapeGeometry.Create(),
      },
    };
    OverlayElementMigration.MigrateFormulas(layouts);
    List<OverlayTextRun> runs = layouts[0].Text.Runs;
    string text = Describe(runs);
    Check(
      text.StartsWith(
        "[progress-start|custom]'%-'[progress-current|custom]'%'[time-current|custom]'~'[time-total|custom]"
      ),
      "progress and time become their parts: " + text
    );
    Check(text.Contains("[accuracy|custom]'%'[combo|custom]"), "lost units come back as text after their value");
    Check(runs[0].Size == 30 && runs[1].Size == 30 && runs[1].Color.Hex == "#123456", "parts keep size and color");
    Check(
      runs.Count(run => run.IsValue && run.Value == "miss") == 1 && runs.Last().Value == "miss",
      "the judgment row becomes counts"
    );
    Check(runs.Find(run => run.Value == "too-early").Color.Hex == "#FF0000", "counts keep their judgment colors");
    Check(layouts[1].Shape.Points.Count == 4, "shapes are untouched");
  }

  // ElementSettings runs Migrate for settings from before 1.1 and
  // MigrateFormulas only for 1.1.0's; the UI harness checks that gate.
  private static void SettingsMigrateOnceByVersion()
  {
    List<OverlayLayoutSettings> layouts = new();
    OverlayElementMigration.Migrate(layouts);
    OverlayLayoutSettings accuracy = layouts.Find(layout => layout.Id == "accuracy");
    Check(
      Describe(accuracy.Text.Runs) == "[accuracy-label|custom]' | '[accuracy|highlight]'%'",
      "presets from before 1.1 get one percent sign: " + Describe(accuracy.Text.Runs)
    );
    Check(
      Describe(layouts.Find(layout => layout.Id == "time").Text.Runs)
        == "[time-label|custom]' | '[time-current|highlight]'~'[time-total|highlight]",
      "the time preset is elapsed ~ total"
    );
  }
}
