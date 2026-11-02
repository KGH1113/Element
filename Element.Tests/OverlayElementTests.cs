using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Xml.Serialization;
using Element.Configuration;
using Element.Domain.Elements;
using Element.Domain.Layout;

namespace Element.Tests;

internal static class OverlayElementTests
{
  public static void Run()
  {
    ValueCatalogKeysAreUniqueAndIndexed();
    MigratesBuiltInsToText();
    MigratesFreshInstallToDefaults();
    ProgressBarIsBuiltIn();
    UpgradesTheFirstProgressBar();
    SplitsTheComboIntoTwoTexts();
    HistoryRestoresTextAndShapeContent();
    ElementSettingsSurviveXml();
    TextDocumentKeepsValuesAtomic();
    TextDocumentMergesRunsAndColors();
    TextDocumentWordsAndCharacters();
    TextDocumentKeepsSizes();
    LegacyLabelsBecomeWords();
    ShapeDefaultsAndEdits();
    GrowFromKeepsItsPoint();
    ShapeRadiiStayInsideTheCorner();
    ShapeOutlinesTriangulate();
    OverlappingCutoutsMerge();
    FormulasParseAndSave();
    FormulasCalculate();
    KeypadEditsFormulas();
    TextDocumentKeepsFormulas();
    MigratesSplitValuesAndUnits();
    SettingsMigrateOnceByVersion();
  }

  private static void FormulasParseAndSave()
  {
    OverlayFormula single = OverlayFormula.Parse("combo");
    Check(single.IsSingleValue(out OverlayValueId id) && id == OverlayValueId.Combo, "a key is one value");
    Check(single.Serialize() == "combo", "one value saves as its key, like 1.1");
    OverlayFormula formula = OverlayFormula.Parse("{combo}*2+{miss}/-4");
    Check(
      formula.Count == 7 && formula.Serialize() == "{combo}*2+{miss}/-4",
      "values, numbers and operators round-trip"
    );
    Check(
      formula.Mask == (OverlayValueCatalog.Bit(OverlayValueId.Combo) | OverlayValueCatalog.Bit(OverlayValueId.Miss)),
      "the mask names its values"
    );
    Check(OverlayFormula.Parse("{gone}*2+{combo}").Serialize() == "2+{combo}", "unknown values drop out");
    Check(OverlayFormula.Parse("{song-title}+1").Serialize() == "1", "words can't take part in arithmetic");
    Check(OverlayFormula.Parse("song-title").IsWords, "words stand alone");
    Check(OverlayFormula.Parse("2 × 3 ÷ 4 − 1").Serialize() == "2*3/4-1", "display symbols read as operators");
    Check(OverlayFormula.Parse("").IsEmpty && OverlayFormula.Parse(null).IsEmpty, "nothing is an empty formula");
    Check(
      OverlayFormula.Parse("12.5").IsPlainNumber(out double number) && number == 12.5,
      "a plain number stays a number"
    );
    Check(
      OverlayFormula.Parse("{time-total}-{time-current}").Describe(value => value.EnglishName)
        == "Total time − Elapsed time",
      "formulas read as words"
    );
  }

  private static void FormulasCalculate()
  {
    Numbers numbers = new()
    {
      [OverlayValueId.Combo] = 120,
      [OverlayValueId.CurrentTime] = 87,
      [OverlayValueId.TotalTime] = 228,
      [OverlayValueId.Miss] = 0,
    };
    Check(Evaluate("2+3*4", numbers, out bool time) == 14 && !time, "× before +");
    Check(Evaluate("10-4/2", numbers, out _) == 8, "÷ before −");
    Check(Evaluate("{combo}*2", numbers, out _) == 240, "values calculate");
    Check(Evaluate("-5+1", numbers, out _) == -4, "a leading minus is a negative number");
    Check(Evaluate("{combo}*", numbers, out _) == 120, "a trailing operator is still being typed");
    Check(Evaluate("{time-total}-{time-current}", numbers, out time) == 141 && time, "time minus time is a time");
    Check(Evaluate("{time-current}*2", numbers, out time) == 174 && time, "a time times a number is a time");
    Check(Evaluate("{time-current}/{time-total}*100", numbers, out time) > 38 && !time, "time over time is a number");
    Check(!OverlayFormula.Parse("{combo}/{miss}").TryEvaluate(numbers, out _, out _), "dividing by zero shows nothing");
    numbers.Remove(OverlayValueId.Combo);
    Check(!OverlayFormula.Parse("{combo}+1").TryEvaluate(numbers, out _, out _), "a value without data shows nothing");
    Check(!OverlayFormula.Parse("song-title").TryEvaluate(numbers, out _, out _), "words have no number");
  }

  private static void KeypadEditsFormulas()
  {
    OverlayFormula formula = OverlayFormula.FromValue(OverlayValueId.Combo);
    Check(!formula.CanType('7'), "a number can't follow a value without an operator");
    Check(formula.Type('*') && formula.AwaitsOperand, "an operator waits for what comes next");
    Check(formula.Type('/') && formula.Serialize() == "{combo}/", "another operator replaces it");
    formula.Type('2');
    formula.Type('.');
    Check(!formula.CanType('.') && formula.Type('5') && formula.Serialize() == "{combo}/2.5", "one point per number");
    Check(formula.Backspace() && formula.Serialize() == "{combo}/2.", "backspace removes a digit");
    formula.Backspace();
    formula.Backspace();
    Check(formula.Serialize() == "{combo}/", "then the number, then the operator");
    Check(formula.CanJoin(OverlayValueId.Miss), "a value can join after an operator");
    formula.Drop(OverlayValueId.Miss);
    Check(formula.Serialize() == "{combo}/{miss}", "a dropped value joins");
    formula.Drop(OverlayValueId.Fps);
    Check(formula.Serialize() == "fps", "without an operator a dropped value replaces the formula");
    Check(!formula.CanJoin(OverlayValueId.SongTitle), "words never join arithmetic");
    OverlayFormula typed = new();
    Check(
      !typed.CanType('+') && typed.Type('-') && typed.Type('0') && typed.Type('7'),
      "a minus starts a negative number"
    );
    Check(typed.Serialize() == "-7", "a leading zero gives way: " + typed.Serialize());
    typed.Type('+');
    Check(
      typed.Trimmed().Serialize() == "-7" && typed.Serialize() == "-7+",
      "trimming drops what is still being typed"
    );
  }

  private static void TextDocumentKeepsFormulas()
  {
    OverlayTextDocument document = new();
    int caret = document.InsertValue(0, 0, "{combo}*2", OverlayColorSettings.HighlightColor(), 0, "{combo}/10");
    Check(caret == 1 && document[0].Value == "{combo}*2", "a calculated value is one character");
    document.SetValue(0, "{combo}+{miss}");
    Check(document[0].Value == "{combo}+{miss}", "the keypad changes a value's formula");
    document.InsertText(1, 1, " hits", null, 0, "{combo}/10");
    List<OverlayTextRun> runs = document.ToRuns();
    Check(
      runs.Count == 2 && runs[0].SizeFormula == "{combo}/10" && runs[1].SizeFormula == "{combo}/10",
      "size formulas save"
    );
    document.SetAlpha(0, document.Count, .5f, "{progress-current}");
    Check(
      document[2].Color.AlphaFormula == "progress-current" && document[2].Color.Alpha == .5f,
      "opacity formulas save"
    );
    OverlayTextDocument loaded = OverlayTextDocument.FromRuns(document.ToRuns());
    Check(loaded.SameAs(document), "formulas round-trip through runs");
    document.SetValue(0, "");
    Check(document.Count == 5 && !document[0].IsValue, "an emptied value is removed");
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

  private static double Evaluate(string text, Numbers numbers, out bool time)
  {
    if (!OverlayFormula.Parse(text).TryEvaluate(numbers, out double result, out time))
      throw new InvalidOperationException(text + " should calculate");
    return result;
  }

  private sealed class Numbers : Dictionary<OverlayValueId, double>, IOverlayNumberSource
  {
    public bool TryGetNumber(OverlayValueId id, out double number) => TryGetValue(id, out number);
  }

  private static void OverlappingCutoutsMerge()
  {
    List<LayoutExclusion> exclusions = new()
    {
      new(new LayoutBounds(0, 400, 600, 100), 8),
      new(new LayoutBounds(100, 300, 200, 80), 8),
      new(new LayoutBounds(500, 450, 200, 100), 4),
      new(new LayoutBounds(0, 0, 0, 10), 8),
    };
    LayoutExclusions.Merge(exclusions);
    Check(exclusions.Count == 2, "separate panels stay separate and empty ones drop: " + exclusions.Count);
    LayoutExclusion merged = exclusions.Find(exclusion => exclusion.Bounds.Y == 400);
    Check(
      merged.Bounds.X == 0 && merged.Bounds.Width == 700 && merged.Bounds.Height == 150 && merged.Radius == 4,
      "overlapping panels become one cutout with the smaller radius"
    );
  }

  private static void ValueCatalogKeysAreUniqueAndIndexed()
  {
    Check(OverlayValueCatalog.All.Count == OverlayValueCatalog.Count, "every value id has a definition");
    Check(OverlayValueCatalog.Count <= 64, "value masks fit one ulong");
    Check(
      OverlayValueCatalog.All.Select(value => value.Key).Distinct().Count() == OverlayValueCatalog.Count,
      "value keys are unique"
    );
    foreach (OverlayValueDefinition value in OverlayValueCatalog.All)
    {
      Check(OverlayValueCatalog.TryGet(value.Key, out OverlayValueDefinition found) && found == value, value.Key);
      Check(!string.IsNullOrEmpty(value.Sample), value.Key + " has a sample for the editor");
    }
    foreach (OverlayDefaultElement element in OverlayElementDefaults.All)
    foreach (OverlayTextRun run in element.CreateRuns())
      Check(!run.IsValue || OverlayValueCatalog.TryGet(run.Value, out _), element.Id + " uses known values");
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
    Check(
      layouts[edge].Enabled
        && layouts[track].Enabled
        && layouts[fill].Enabled
        && layouts[fill].Shape.WidthFormula == progress.WidthFormula,
      "Reset all brings the progress bar back"
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

  private static void HistoryRestoresTextAndShapeContent()
  {
    OverlayLayoutSettings text = OverlayElementDefaults.Create(OverlayElementDefaults.All[0]);
    OverlayLayoutSettings shape = new()
    {
      Id = "shape:1",
      TemplateId = OverlayElementKinds.Shape,
      Shape = OverlayShapeGeometry.Create(),
    };
    List<OverlayLayoutSettings> values = new() { text, shape };
    LayoutHistory history = new();
    history.Reset(values);
    text.Text.Runs[1].Text = " :: ";
    shape.Shape.WidthFormula = "{combo}*2";
    shape.Shape.Points[0].Radius = 12;
    shape.Shape.Fill = OverlayColorSettings.Custom("#00FF00", .5f);
    history.Commit(values);
    Check(history.CanUndo, "content edits are undoable");
    history.Undo(values);
    Check(text.Text.Runs[1].Text == " | ", "undo restores text");
    Check(
      shape.Shape.Points[0].Radius == 0 && shape.Shape.Fill.Highlight && shape.Shape.WidthFormula == "",
      "undo restores the shape and its formulas"
    );
    history.Redo(values);
    Check(text.Text.Runs[1].Text == " :: " && shape.Shape.Fill.Hex == "#00FF00", "redo reapplies content");
    shape.Shape.Fill.Hex = "#00FF00";
    history.Commit(values);
    Check(!history.CanRedo, "an identical state is not a new step");
  }

  private static void ElementSettingsSurviveXml()
  {
    OverlayLayoutSettings original = new()
    {
      Id = "shape:1",
      TemplateId = OverlayElementKinds.Shape,
      Customized = true,
      Shape = OverlayShapeGeometry.Create(OverlayShapeKind.Triangle),
      Text = new OverlayTextSettings
      {
        Runs = { OverlayTextRun.Literal("<b>&", OverlayColorSettings.Custom("#123456", .25f)) },
      },
    };
    original.Text.Runs[0].Size = 33;
    original.Text.Runs[0].SizeFormula = "{combo}/10";
    original.Text.Runs[0].Color.AlphaFormula = "{progress-current}";
    original.Shape.Points[2].Radius = 7;
    original.Shape.Points[1].RadiusFormula = "{combo}";
    original.Shape.StrokeWidth = 3;
    original.Shape.HeightFormula = "{fps}/2";
    XmlSerializer serializer = new(typeof(OverlayLayoutSettings));
    using StringWriter writer = new();
    serializer.Serialize(writer, original);
    using StringReader reader = new(writer.ToString());
    OverlayLayoutSettings loaded = (OverlayLayoutSettings)serializer.Deserialize(reader);
    Check(loaded.SameAs(original), "text, sizes, shapes and formulas round-trip through the settings file");
    Check(
      loaded.Shape.Points[1].RadiusFormula == "{combo}" && loaded.Shape.HeightFormula == "{fps}/2",
      "formulas load back"
    );
  }

  private static void TextDocumentKeepsValuesAtomic()
  {
    OverlayTextDocument document = new();
    OverlayColorSettings white = OverlayColorSettings.Custom("#FFFFFF");
    int caret = document.InsertText(0, 0, "FPS | ", white);
    caret = document.InsertValue(caret, caret, "fps", OverlayColorSettings.HighlightColor());
    Check(caret == 7 && document.Count == 7, "a value is one character");
    caret = document.InsertText(caret, caret, "!", document.TypingColor(caret));
    Check(document[7].Color.SameAs(white), "typing after a value continues the text color, not the value's");
    document.Delete(6, 7);
    Check(document.Count == 7 && !document[6].IsValue, "deleting one step removes the whole value");
    document.InsertValue(0, 3, "kps", null);
    Check(document[0].Value == "kps" && document.Count == 5, "a value replaces the selected text");
    Check(document.InsertValue(0, 0, "unknown", null) == 0 && document.Count == 5, "unknown values are ignored");
    Check(
      document.InsertValue(0, 0, "{combo}+1", null) == 1 && document[0].Value == "{combo}+1",
      "formulas are values too"
    );
  }

  private static void TextDocumentMergesRunsAndColors()
  {
    OverlayTextDocument document = OverlayTextDocument.FromRuns(OverlayElementDefaults.All[3].CreateRuns());
    Check(document.Count == 7, "FPS | {fps} has seven characters");
    document.SetColor(0, 3, OverlayColorSettings.HighlightColor(.5f));
    List<OverlayTextRun> runs = document.ToRuns();
    Check(Describe(runs) == "'FPS'' | '[fps|highlight]", "recolored text splits into runs: " + Describe(runs));
    Check(runs[0].Color.Highlight && runs[0].Color.Alpha == .5f, "the selection keeps its new opacity");
    document.SetColor(0, 3, OverlayColorSettings.Custom("#FFFFFF"));
    Check(Describe(document.ToRuns()) == "'FPS | '[fps|highlight]", "same-colored text merges back");
    OverlayTextDocument copy = document.Clone();
    copy.Delete(0, 1);
    Check(document.Count == 7 && !copy.SameAs(document), "clones are independent");
  }

  private static void TextDocumentKeepsSizes()
  {
    OverlayTextDocument document = new();
    int caret = document.InsertText(0, 0, "Big", null, 40);
    document.InsertText(caret, caret, " small", null);
    Check(document.TypingSize(3) == 40 && document.TypingSize(9) == 0, "typing keeps the nearest text's size");
    List<OverlayTextRun> runs = document.ToRuns();
    Check(runs.Count == 2 && runs[0].Size == 40 && runs[1].Size == 0, "sizes split runs: " + runs.Count);
    document.SetSize(0, document.Count, 30);
    runs = document.ToRuns();
    Check(runs.Count == 1 && runs[0].Size == 30 && runs[0].Text == "Big small", "one size merges the runs again");
    document.SetSize(0, 3, float.NaN);
    Check(document[0].Size == 0, "invalid sizes fall back to the base size");
    Check(OverlayTextDocument.FromRuns(runs).SameAs(OverlayTextDocument.FromRuns(runs)), "sizes round-trip");
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
      OverlayValueCatalog.All.All(value => value.Key != "progress" && value.Key != "time" && value.Key != "judgements"),
      "values made of other values are gone"
    );
    Check(
      OverlayValueCatalog.All.Where(value => value.IsNumeric).All(value => !value.Sample.Contains("%")),
      "values have no units"
    );
  }

  private static void TextDocumentWordsAndCharacters()
  {
    OverlayTextDocument document = new();
    document.InsertText(0, 0, "a😀b\r\nc\u0001", null);
    Check(
      document.Count == 5 && document[1].Text == "😀" && document[3].Text == "\n",
      "pairs and line ends are one step"
    );
    document = new OverlayTextDocument();
    int caret = document.InsertText(0, 0, "Best combo ", null);
    document.InsertValue(caret, caret, "combo", null);
    Check(document.WordStart(document.Count) == document.Count - 1, "a value is its own word");
    Check(document.WordStart(10) == 5 && document.WordEnd(0) == 4, "words skip spaces");
    Check(
      document.PlainText(0, document.Count, key => "{" + key + "}") == "Best combo {combo}",
      "copied text names its values"
    );
  }

  private static void ShapeDefaultsAndEdits()
  {
    OverlayShapeSettings shape = OverlayShapeGeometry.Create();
    Check(shape.Points.Count == 4 && shape.Width == OverlayShapeGeometry.DefaultSize, "a new square has four corners");
    Check(OverlayShapeGeometry.Create(OverlayShapeKind.Triangle).Points.Count == 3, "a triangle has three");
    Check(OverlayShapeGeometry.Create(OverlayShapeKind.Circle).Points.Count == 0, "circles have no corners");
    OverlayShapeSettings damaged = new()
    {
      Kind = OverlayShapeKind.Triangle,
      Width = float.NaN,
      StrokeWidth = -2,
    };
    OverlayShapeGeometry.Normalize(damaged);
    Check(
      damaged.Points.Count == 3 && damaged.Width == OverlayShapeGeometry.DefaultSize && damaged.StrokeWidth == 0,
      "damaged settings are repaired"
    );
  }

  // A chosen grow point stays put when the size changes; Auto uses the anchor's point.
  private static void GrowFromKeepsItsPoint()
  {
    LayoutBounds bounds = new(300, 200, 100, 40);
    AnchoredLayoutPosition left = OverlayLayoutPosition.Capture(bounds, 1920, 1080, Anchor9.Center, OverlayGrowth.Left);
    OverlayLayoutSettings settings = new()
    {
      Customized = true,
      LayoutVersion = OverlayLayoutPosition.CurrentVersion,
      Anchor = left.Anchor,
      OffsetX = left.OffsetX,
      OffsetY = left.OffsetY,
      GrowFrom = OverlayGrowth.Left,
    };
    LayoutBounds wider = OverlayLayoutPosition.Resolve(settings, 300, 40, 1920, 1080);
    Check(Near(wider.X, 300) && Near(wider.Y, 200), "growing from the left keeps the left edge: " + wider.X);
    settings.GrowFrom = OverlayGrowth.BottomRight;
    AnchoredLayoutPosition corner = OverlayLayoutPosition.Capture(
      bounds,
      1920,
      1080,
      Anchor9.Center,
      OverlayGrowth.BottomRight
    );
    settings.OffsetX = corner.OffsetX;
    settings.OffsetY = corner.OffsetY;
    LayoutBounds bigger = OverlayLayoutPosition.Resolve(settings, 200, 80, 1920, 1080);
    Check(
      Near(bigger.X + bigger.Width, 400) && Near(bigger.Y + bigger.Height, 240),
      "growing from a corner keeps that corner"
    );
    Check(
      OverlayLayoutPosition.GrowPoint(OverlayGrowth.Auto, Anchor9.TopRight) == Anchor9.TopRight
        && OverlayLayoutPosition.GrowPoint(OverlayGrowth.Top, Anchor9.TopRight) == Anchor9.Top,
      "Auto grows from the anchor's point; a choice overrides it"
    );
  }

  private static void ShapeRadiiStayInsideTheCorner()
  {
    List<Vector2> square = new() { new(0, 0), new(100, 0), new(100, 100), new(0, 100) };
    Check(Near(OverlayShapeGeometry.MaxRadius(square, 0), 50), "a square's corner rounds to half its side");
    Check(OverlayShapeGeometry.ClampRadius(square, 0, 80) == OverlayShapeGeometry.MaxRadius(square, 0), "radii clamp");
    Check(OverlayShapeGeometry.ClampRadius(square, 0, -3) == 0, "negative radii clear");
    List<Vector2> straight = new() { new(0, 0), new(50, 0), new(100, 0), new(50, 50) };
    Check(OverlayShapeGeometry.MaxRadius(straight, 1) == 0, "a straight corner can't round");
  }

  private static void ShapeOutlinesTriangulate()
  {
    List<Vector2> square = new() { new(0, 0), new(100, 0), new(100, 100), new(0, 100) };
    List<Vector2> outline = new();
    OverlayShapeGeometry.Outline(square, new float[] { 0, 30, 0, 0 }, .5f, outline);
    Check(outline.Count > 5, "a rounded corner adds arc points");
    Check(
      outline.Contains(new Vector2(0, 0)) && !outline.Contains(new Vector2(100, 0)),
      "only the rounded corner is replaced"
    );
    Check(Near(outline[1].X, 70) && Near(outline[1].Y, 0), "the arc starts tangent to the edge");
    List<int> triangles = new();
    OverlayShapeGeometry.Triangulate(outline, triangles);
    Check(
      Near(Area(outline, triangles), Math.Abs(OverlayShapeGeometry.SignedArea(outline))),
      "convex outlines fill exactly"
    );
    List<Vector2> arrow = new() { new(0, 0), new(100, 0), new(40, 40), new(100, 100), new(0, 100) };
    OverlayShapeGeometry.Triangulate(arrow, triangles);
    Check(triangles.Count == 9, "a concave pentagon becomes three triangles");
    Check(
      Near(Area(arrow, triangles), Math.Abs(OverlayShapeGeometry.SignedArea(arrow))),
      "concave outlines fill exactly"
    );
    OverlayShapeGeometry.Ellipse(200, 100, 32, outline);
    Check(outline.Count == 32 && Near(outline[0].X, 200) && Near(outline[0].Y, 50), "ellipses fill their box");
  }

  private static float Area(List<Vector2> points, List<int> triangles)
  {
    float area = 0;
    for (int i = 0; i < triangles.Count; i += 3)
    {
      Vector2 a = points[triangles[i]],
        b = points[triangles[i + 1]],
        c = points[triangles[i + 2]];
      area += Math.Abs((b.X - a.X) * (c.Y - a.Y) - (c.X - a.X) * (b.Y - a.Y)) * .5f;
    }
    return area;
  }

  private static string Describe(List<OverlayTextRun> runs) =>
    string.Concat(
      runs.Select(run =>
        run.IsValue
          ? "[" + run.Value + "|" + (run.Color.Highlight ? "highlight" : "custom") + "]"
          : "'" + run.Text + "'"
      )
    );

  private static OverlayLayoutSettings Find(List<OverlayLayoutSettings> layouts, string id) =>
    layouts.Find(layout => layout.Id == id);

  private static bool Near(float a, float b) => Math.Abs(a - b) < .01f;

  private static void Check(bool condition, string message)
  {
    if (!condition)
      throw new InvalidOperationException(message);
  }
}
