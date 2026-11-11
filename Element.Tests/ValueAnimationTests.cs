using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Serialization;
using Element.Application.Settings;
using Element.Configuration;
using Element.Configuration.Overlay;
using Element.Domain.Overlay.Defaults;
using Element.Domain.Overlay.Text;
using Element.Domain.Overlay.Values;
using static Element.Tests.Support.Assert;

namespace Element.Tests;

// A value's animation switch: the editing document, saving, the move from
// "every combo grows" and when a shown value counts as changed.
internal static class ValueAnimationTests
{
  public static void Run()
  {
    SwitchTouchesOnlyValues();
    RangeSaysWhetherItsValuesAnimate();
    NewValuesFollowTheNearestValue();
    AnimationSurvivesRunsAndSaving();
    CombosKeepGrowingAfterTheUpdate();
    ShownChangesCountOnlyNewText();
  }

  private static OverlayTextDocument FpsAndCombo()
  {
    OverlayTextDocument document = new();
    OverlayColorSettings white = OverlayColorSettings.Custom("#FFFFFF");
    int caret = document.InsertText(0, 0, "FPS ", white);
    caret = document.InsertValue(caret, caret, "fps", white);
    caret = document.InsertText(caret, caret, " x", white);
    document.InsertValue(caret, caret, "combo", white);
    return document;
  }

  private static void SwitchTouchesOnlyValues()
  {
    OverlayTextDocument document = FpsAndCombo();
    Check(document.Count == 8 && document[4].IsValue && document[7].IsValue, "FPS {fps} x{combo}");
    List<OverlayTextRun> before = document.ToRuns();
    document.SetAnimate(0, document.Count, true);
    Check(document[4].Animate && document[7].Animate, "the whole text turns every value on");
    Check(
      Enumerable.Range(0, document.Count).All(i => document[i].IsValue || !document[i].Animate),
      "letters never animate"
    );
    List<OverlayTextRun> after = document.ToRuns();
    Check(after.Count == before.Count, "turning values on doesn't split the text between them: " + after.Count);
    document.SetAnimate(5, 8, false);
    Check(document[4].Animate && !document[7].Animate, "a selection turns only its own values off");
    Check(
      !document.SameAs(FpsAndCombo()) && document.Clone().SameAs(document),
      "the switch is part of the text's content"
    );
  }

  private static void RangeSaysWhetherItsValuesAnimate()
  {
    OverlayTextDocument document = FpsAndCombo();
    Check(document.AnimateState(0, 4) == OverlayAnimateState.NoValues, "letters alone have nothing to animate");
    Check(document.AnimateState(0, document.Count) == OverlayAnimateState.Off, "values start still");
    document.SetAnimate(7, 8, true);
    Check(document.AnimateState(0, document.Count) == OverlayAnimateState.Mixed, "one on and one off is mixed");
    Check(document.AnimateState(8, 5) == OverlayAnimateState.On, "a backwards selection reads the same range");
    Check(document.AnimateState(3, 3) == OverlayAnimateState.NoValues, "an empty range has no values");
  }

  private static void NewValuesFollowTheNearestValue()
  {
    OverlayTextDocument document = FpsAndCombo();
    document.SetAnimate(7, 8, true);
    Check(document.TypingAnimate(8), "after an animated value, a new one animates");
    Check(!document.TypingAnimate(5), "after a still value, a new one stays still");
    Check(!document.TypingAnimate(0), "before every value, the first one decides");
    OverlayTextDocument letters = new();
    letters.InsertText(0, 0, "Perfect", OverlayColorSettings.Custom("#FFFFFF"));
    Check(!letters.TypingAnimate(3), "a text without values adds still ones");
    int caret = document.InsertValue(8, 8, "miss", null, animate: document.TypingAnimate(8));
    Check(caret == 9 && document[8].Animate, "the editor passes it on");
  }

  private static void AnimationSurvivesRunsAndSaving()
  {
    OverlayTextDocument document = FpsAndCombo();
    document.SetAnimate(7, 8, true);
    List<OverlayTextRun> runs = document.ToRuns();
    Check(runs.Single(run => run.Value == "combo").Animate, "the run keeps the switch");
    Check(
      !runs.Single(run => run.Value == "fps").Animate && runs.Where(run => !run.IsValue).All(run => !run.Animate),
      "others stay still"
    );
    Check(OverlayTextDocument.FromRuns(runs).SameAs(document), "reading the runs back gives the same text");
    OverlayTextRun combo = runs.Single(run => run.Value == "combo");
    Check(combo.Clone().SameAs(combo) && combo.Clone().Animate, "a copy keeps it");
    OverlayTextRun still = combo.Clone();
    still.Animate = false;
    Check(!still.SameAs(combo), "the switch counts as a change to the text");

    List<OverlayLayoutSettings> layouts = new()
    {
      new OverlayLayoutSettings
      {
        Id = "text:a",
        TemplateId = OverlayElementKinds.Text,
        Text = new OverlayTextSettings { Runs = runs },
      },
    };
    using MemoryStream file = new();
    using (StreamWriter writer = new(file, new UTF8Encoding(false), 1024, true))
      new XmlSerializer(typeof(List<OverlayLayoutSettings>)).Serialize(writer, layouts);
    file.Position = 0;
    List<OverlayLayoutSettings> loaded = SettingsXml.Read<List<OverlayLayoutSettings>>(file);
    Check(loaded[0].SameAs(layouts[0]), "the switch is saved and read back");
  }

  private static void CombosKeepGrowingAfterTheUpdate()
  {
    Check(
      OverlayElementDefaults.TryGet(OverlayElementDefaults.ComboId, out OverlayDefaultElement element)
        && element.CreateRuns().Single().Animate,
      "the built-in combo grows on every hit"
    );
    Check(
      OverlayElementDefaults
        .All.Where(other => other.Id != OverlayElementDefaults.ComboId)
        .SelectMany(other => other.CreateRuns())
        .All(run => !run.Animate),
      "no other built-in moves"
    );

    ElementSettings settings = new() { SettingsVersion = 8, OverlayLayouts = new() };
    OverlayLayoutSettings text = new()
    {
      Id = "text:combo",
      TemplateId = OverlayElementKinds.Text,
      Text = new OverlayTextSettings
      {
        Runs =
        {
          OverlayTextRun.Literal("x", OverlayColorSettings.Custom("#FFFFFF")),
          OverlayTextRun.ValueRun("{combo}*2", OverlayColorSettings.HighlightColor()),
          OverlayTextRun.ValueRun("miss", OverlayColorSettings.HighlightColor()),
        },
      },
    };
    settings.OverlayLayouts.Add(text);
    SettingsMigrator.Migrate(settings);
    List<OverlayTextRun> runs = settings.OverlayLayouts.Single(layout => layout.Id == "text:combo").Text.Runs;
    Check(runs[1].Animate, "a combo, even calculated, keeps growing after the update");
    Check(!runs[0].Animate && !runs[2].Animate, "nothing else starts moving");
    Check(settings.SettingsVersion == ElementSettings.CurrentSettingsVersion, "the settings are current");

    runs[1].Animate = false;
    SettingsMigrator.Migrate(settings);
    Check(!runs[1].Animate, "a combo turned off stays off on the next load");
  }

  private static void ShownChangesCountOnlyNewText()
  {
    OverlayShownValues shown = new();
    StringBuilder text = new("ab120cd");
    Check(!shown.Update(0, text, 2, 3), "the first text only records");
    Check(!shown.Update(0, text, 2, 3), "the same text is no change");
    text.Clear().Append("ab121cd");
    Check(shown.Update(0, text, 2, 3), "new text is a change");
    Check(!shown.Update(0, text, 2, 0), "going away is no change");
    Check(!shown.Update(0, text, 2, 3), "appearing again is no change");
    text.Clear().Append("ab1210");
    Check(shown.Update(0, text, 2, 4), "a longer text is a change");
    Check(!shown.Update(1, text, 0, 2), "each slot counts on its own");
    shown.Clear();
    text.Clear().Append("ab999");
    Check(!shown.Update(0, text, 2, 3), "after clearing, the first text only records");
    Check(!shown.Update(-1, text, 0, 1), "no slot, no change");
  }
}
