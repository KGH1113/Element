using System.Collections.Generic;
using System.IO;
using System.Xml.Serialization;
using Element.Configuration;
using Element.Configuration.Overlay;
using Element.Domain.Layout;
using Element.Domain.Overlay.Defaults;
using Element.Domain.Overlay.Shape;
using static Element.Tests.Support.Assert;

namespace Element.Tests.Domain.Overlay;

/// <summary>Element settings survive undo and saving, spaces between values included.</summary>
internal static class OverlaySavingTests
{
  internal static void Run()
  {
    HistoryRestoresTextAndShapeContent();
    ElementSettingsSurviveXml();
    SpacesBetweenValuesSurviveSaving();
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

  // A space on its own between two values, as in the judgment row, is a run
  // of only whitespace; reading the settings file back must keep it.
  private static void SpacesBetweenValuesSurviveSaving()
  {
    List<OverlayLayoutSettings> layouts = new();
    OverlayLayoutSettings judgement = new()
    {
      Id = "judgement",
      Customized = true,
      Text = new OverlayTextSettings(),
    };
    judgement.Text.Runs.AddRange(OverlayDefaultRuns.Judgements(OverlayColorSettings.Custom("#D958FF"), 0));
    judgement.Text.Runs.Add(OverlayTextRun.Literal("  ", OverlayColorSettings.HighlightColor()));
    layouts.Add(judgement);
    using MemoryStream file = new();
    using (StreamWriter writer = new(file, new System.Text.UTF8Encoding(false), 1024, true))
      new XmlSerializer(typeof(List<OverlayLayoutSettings>)).Serialize(writer, layouts);
    file.Position = 0;
    List<OverlayLayoutSettings> loaded = SettingsXml.Read<List<OverlayLayoutSettings>>(file);
    List<OverlayTextRun> runs = loaded[0].Text.Runs;
    Check(
      runs.Count == judgement.Text.Runs.Count && runs[1].Text == " " && runs[^1].Text == "  ",
      "spaces between values come back after saving: '" + runs[1].Text + "'"
    );
    Check(loaded[0].SameAs(judgement), "the rest of the text comes back as it was");
  }
}
