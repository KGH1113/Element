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
    SplitsTheComboIntoTwoTexts();
    HistoryRestoresTextAndShapeContent();
    ElementSettingsSurviveXml();
    TextDocumentKeepsValuesAtomic();
    TextDocumentMergesRunsAndColors();
    TextDocumentWordsAndCharacters();
    TextDocumentKeepsSizes();
    LegacyLabelsBecomeWords();
    ShapeDefaultsAndEdits();
    ShapeCornersGrowTheBox();
    ShapeRadiiStayInsideTheCorner();
    ShapeOutlinesTriangulate();
    ShapeSidesStretchOneWay();
    OverlappingCutoutsMerge();
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

  private static void ShapeSidesStretchOneWay()
  {
    LayoutBounds start = new(100, 50, 200, 100);
    LayoutBounds right = OverlayLayoutGeometry.Stretch(start, LayoutHandle.Right, 40, 30, 4);
    Check(
      right.X == 100 && right.Width == 240 && right.Height == 100 && right.Y == 50,
      "a side moves only its own edge"
    );
    LayoutBounds top = OverlayLayoutGeometry.Stretch(start, LayoutHandle.Top, 0, 500, 4);
    Check(top.Bottom == 150 && top.Height == 4, "a side cannot pass the opposite one");
    LayoutBounds corner = OverlayLayoutGeometry.Stretch(start, LayoutHandle.TopLeft, 10, 10, 4);
    Check(corner.X == start.X && corner.Width == start.Width, "corners keep proportional resizing");
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
      Describe(progress.Text.Runs) == "[progress-label|custom]' | '[progress|highlight]",
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
        && layouts.All(layout =>
          layout.TemplateId == OverlayElementKinds.Text
          && layout.Enabled
          && layout.DefaultPlacement == layout.Id
          && !layout.Customized
        ),
      "a fresh install starts with every built-in text shown in its place"
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
    shape.Shape.Points[0].Radius = 12;
    shape.Shape.Fill = OverlayColorSettings.Custom("#00FF00", .5f);
    history.Commit(values);
    Check(history.CanUndo, "content edits are undoable");
    history.Undo(values);
    Check(text.Text.Runs[1].Text == " | ", "undo restores text");
    Check(shape.Shape.Points[0].Radius == 0 && shape.Shape.Fill.Highlight, "undo restores the shape");
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
    original.Shape.Points[2].Radius = 7;
    original.Shape.StrokeWidth = 3;
    XmlSerializer serializer = new(typeof(OverlayLayoutSettings));
    using StringWriter writer = new();
    serializer.Serialize(writer, original);
    using StringReader reader = new(writer.ToString());
    OverlayLayoutSettings loaded = (OverlayLayoutSettings)serializer.Deserialize(reader);
    Check(loaded.SameAs(original), "text, sizes and shape settings round-trip through the settings file");
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
      document.Count == 7 && !document[0].IsValue && document[0].Text == "진" && document[6].Value == "progress",
      "a converted label becomes plain words; other values stay"
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
    Check(shape.Points.Count == 4 && !OverlayShapeGeometry.IsEdited(shape), "a new square is unedited");
    shape.Width = 300;
    Check(!OverlayShapeGeometry.IsEdited(shape), "resizing the box is not a vertex edit");
    shape.Points[1].Radius = 4;
    Check(OverlayShapeGeometry.IsEdited(shape), "rounding a corner is an edit");
    shape.Fill = OverlayColorSettings.Custom("#000000", .5f);
    OverlayShapeGeometry.SetKind(shape, OverlayShapeKind.Triangle);
    Check(
      shape.Points.Count == 3 && shape.Width == 300 && !shape.Fill.Highlight && !OverlayShapeGeometry.IsEdited(shape),
      "changing the kind keeps the box and colors but drops edits"
    );
    OverlayShapeGeometry.SetKind(shape, OverlayShapeKind.Circle);
    Check(shape.Points.Count == 0 && !OverlayShapeGeometry.IsEdited(shape), "circles have no corners");
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

  private static void ShapeCornersGrowTheBox()
  {
    OverlayShapeSettings shape = OverlayShapeGeometry.Create();
    List<Vector2> corners = new();
    OverlayShapeGeometry.Corners(shape, corners);
    OverlayShapeGeometry.MoveCorner(shape, corners, 0, new Vector2(-40, 20), out Vector2 origin);
    Check(origin == new Vector2(-40, 0), "dragging left of the box moves its left edge");
    Check(shape.Width == 200 && shape.Height == 160, "the box spans every corner");
    Check(
      Near(shape.Points[0].X, 0) && Near(shape.Points[0].Y, 20f / 160) && Near(shape.Points[1].X, 1),
      "corners renormalize"
    );
    List<Vector2> moved = new();
    OverlayShapeGeometry.Corners(shape, moved);
    Check(Near(moved[2].X + origin.X, 160) && Near(moved[2].Y + origin.Y, 160), "other corners stay put on screen");
    OverlayShapeGeometry.MoveCorner(shape, moved, 0, moved[1], out _);
    Check(shape.Width >= OverlayShapeGeometry.MinimumSize, "a collapsed edge keeps a drawable box");
  }

  private static void ShapeRadiiStayInsideTheCorner()
  {
    List<Vector2> square = new() { new(0, 0), new(100, 0), new(100, 100), new(0, 100) };
    Check(Near(OverlayShapeGeometry.MaxRadius(square, 0), 50), "a square's corner rounds to half its side");
    Check(OverlayShapeGeometry.ClampRadius(square, 0, 80) == OverlayShapeGeometry.MaxRadius(square, 0), "radii clamp");
    Check(OverlayShapeGeometry.TryRadiusHandle(square, 0, 20, 10, out Vector2 handle), "corners have a radius handle");
    Check(handle.X > 0 && Near(handle.X, handle.Y), "the handle sits on the inner bisector");
    Check(
      Near(OverlayShapeGeometry.RadiusFromHandle(square, 0, handle, 10), 20),
      "dragging the handle reads back its radius"
    );
    Check(OverlayShapeGeometry.RadiusFromHandle(square, 0, new Vector2(-5, -5), 10) == 0, "dragging outward clears it");
    List<Vector2> straight = new() { new(0, 0), new(50, 0), new(100, 0), new(50, 50) };
    Check(!OverlayShapeGeometry.TryRadiusHandle(straight, 1, 0, 10, out _), "a straight corner has no handle");
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
