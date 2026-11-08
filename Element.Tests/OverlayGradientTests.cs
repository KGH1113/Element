using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Serialization;
using Element.Configuration;
using Element.Domain.Elements;

namespace Element.Tests;

internal static class OverlayGradientTests
{
  public static void Run()
  {
    ASecondColorMakesAGradientThatShowsAtOnce();
    TheHighlightFadesInsteadOfChangingColor();
    RemovingDownToOneColorIsSolidAgain();
    ColorsMixWithTheirOpacity();
    EachKindMeasuresItsOwnWay();
    AnglesTurnLikeElementNote();
    FlipRotateAndKindKeepTheGradientUsable();
    SwitchingToTheHighlightKeepsAFade();
    StopsSnapToQuartersAndEachOther();
    GradientsSurviveTheSettingsFileAndOldVersions();
  }

  private static void ASecondColorMakesAGradientThatShowsAtOnce()
  {
    OverlayColorSettings color = OverlayColorSettings.Custom("#BA712B");
    int added = OverlayGradientEdits.AddStop(color, .7f, "#BA712B");
    Check(color.IsGradient && added == 1 && color.Gradient.Stops.Count == 2, "a second color makes a gradient");
    OverlayGradientStop stop = color.Gradient.Stops[added];
    Check(stop.Alpha == 1, "the new color starts opaque, not see-through");
    Check(stop.Hex != "#BA712B", "the new color is a neighbor, so the gradient shows at once: " + stop.Hex);
    Check(Near(stop.Position, .7f) && color.Gradient.Stops[0].Position == 0, "it lands where the bar was clicked");
    OverlayColorSettings gray = OverlayColorSettings.Custom("#FFFFFF");
    OverlayGradientEdits.AddStop(gray, .5f, "#4F8DF7");
    Check(gray.Gradient.Stops[1].Hex == "#4F8DF7", "white has no hue to turn, so it meets the highlight color");
    OverlayColorSettings edge = OverlayColorSettings.Custom("#123456");
    OverlayGradientEdits.AddStop(edge, .02f, "#BA712B");
    Check(edge.Gradient.Stops[0].Position == 1, "a click on the color itself moves it to the other end");
    for (int i = 0; i < 10; i++)
      OverlayGradientEdits.AddStop(color, i / 10f, "#BA712B");
    Check(color.Gradient.Stops.Count == OverlayGradient.MaximumStops, "a gradient holds at most eight colors");
    OverlayColorSettings middle = OverlayColorSettings.Custom("#000000");
    OverlayGradientEdits.AddStop(middle, 1, "#BA712B");
    middle.Gradient.Stops[1].Hex = "#FFFFFF";
    int third = OverlayGradientEdits.AddStop(middle, .5f, "#BA712B");
    Check(middle.Gradient.Stops[third].Hex == "#808080", "later colors start as the gradient's color there");
  }

  private static void TheHighlightFadesInsteadOfChangingColor()
  {
    OverlayColorSettings color = OverlayColorSettings.HighlightColor();
    OverlayGradientEdits.AddStop(color, 1, "#BA712B");
    Check(color.Highlight && color.IsGradient, "the highlight color makes a gradient too");
    Check(
      Near(color.Gradient.Stops[1].Alpha, OverlayGradientEdits.HighlightFade),
      "its second color is the highlight at a lighter opacity"
    );
    OverlayColorSettings faint = OverlayColorSettings.HighlightColor(.3f);
    OverlayGradientEdits.AddStop(faint, 1, "#BA712B");
    Check(faint.Gradient.Stops[1].Alpha == 1, "a faint highlight brightens instead");
  }

  private static void RemovingDownToOneColorIsSolidAgain()
  {
    OverlayColorSettings color = OverlayColorSettings.Custom("#112233", .8f);
    OverlayGradientEdits.AddStop(color, 1, "#BA712B");
    OverlayGradientEdits.AddStop(color, .5f, "#BA712B");
    int next = OverlayGradientEdits.RemoveStop(color, 2);
    Check(color.IsGradient && next >= 0, "removing one of three keeps a gradient");
    color.Gradient.Stops[1].Hex = "#ABCDEF";
    color.Gradient.Stops[1].Alpha = .5f;
    OverlayGradientEdits.RemoveStop(color, 0);
    Check(!color.IsGradient && color.Gradient == null, "one color left is solid again");
    Check(color.Hex == "#ABCDEF" && Near(color.Alpha, .5f), "the color left keeps its color and opacity");
    Check(OverlayGradientEdits.RemoveStop(color, 0) == -1, "the last color can't be removed");
  }

  private static void ColorsMixWithTheirOpacity()
  {
    List<OverlayGradientSample> stops =
      new()
      {
        new(0, new OverlayRgba(1, 0, 0, 1)),
        new(1, new OverlayRgba(0, 0, 0, 0)),
      };
    OverlayRgba middle = OverlayGradientMath.Sample(stops, .5f);
    Check(Near(middle.R, 1) && Near(middle.A, .5f), "fading out keeps the color instead of darkening it");
    Check(Near(OverlayGradientMath.Sample(stops, -1).R, 1), "before the first stop is its color");
    Check(Near(OverlayGradientMath.Sample(stops, 2).A, 0), "after the last stop is its color");
    List<OverlayGradientSample> hard =
      new()
      {
        new(.5f, new OverlayRgba(1, 0, 0, 1)),
        new(.5f, new OverlayRgba(0, 0, 1, 1)),
      };
    OverlayGradientMath.Sort(hard);
    Check(hard[0].Color.R == 1, "stops at one place keep their order");
    Check(OverlayGradientMath.Sample(hard, .49f).R == 1 && OverlayGradientMath.Sample(hard, .51f).B == 1, "a hard edge");
  }

  private static void EachKindMeasuresItsOwnWay()
  {
    OverlayGradient gradient = new();
    OverlayGradientMath.ResetHandles(gradient, OverlayGradientKind.Linear);
    OverlayGradientFrame linear = OverlayGradientMath.Frame(gradient, 200, 100);
    Check(Near(linear.At(0, 0), 0) && Near(linear.At(100, 80), .5f) && Near(linear.At(200, 10), 1), "linear runs left to right");
    OverlayGradientMath.ResetHandles(gradient, OverlayGradientKind.Radial);
    OverlayGradientFrame radial = OverlayGradientMath.Frame(gradient, 200, 100);
    Check(Near(radial.At(100, 50), 0) && Near(radial.At(200, 50), 1) && Near(radial.At(100, 100), 1), "radial fills the box");
    gradient.Kind = OverlayGradientKind.Diamond;
    OverlayGradientFrame diamond = OverlayGradientMath.Frame(gradient, 200, 100);
    Check(Near(diamond.At(150, 75), 1) && Near(diamond.At(150, 50), .5f), "diamond adds both directions");
    gradient.Kind = OverlayGradientKind.Angular;
    OverlayGradientFrame angular = OverlayGradientMath.Frame(gradient, 200, 100);
    Check(Near(angular.At(200, 50), 0) && Near(angular.At(100, 100), .25f), "angular turns clockwise from the end handle");
    Check(Near(angular.At(0, 50), .5f) && Near(angular.At(100, 0), .75f), "and comes round");
    gradient.EndX = gradient.StartX;
    gradient.EndY = gradient.StartY;
    Check(!OverlayGradientMath.Frame(gradient, 200, 100).Valid, "handles on one point show the first color");
  }

  private static void AnglesTurnLikeElementNote()
  {
    OverlayGradient gradient = new();
    OverlayGradientMath.ResetHandles(gradient, OverlayGradientKind.Linear);
    Check(Near(OverlayGradientMath.Angle(gradient, 300, 100), 90), "left to right is 90°");
    OverlayGradientMath.SetAngle(gradient, 180, 300, 100);
    Check(Near(OverlayGradientMath.Angle(gradient, 300, 100), 180), "180° runs top to bottom");
    Check(Near(gradient.StartX, (gradient.EndX + gradient.StartX) * .5f), "a linear gradient turns about its middle");
    OverlayGradientMath.ResetHandles(gradient, OverlayGradientKind.Radial);
    OverlayGradientMath.SetAngle(gradient, 0, 200, 200);
    Check(Near(gradient.StartX, .5f) && Near(gradient.StartY, .5f), "the others turn about their center");
    Check(Near(OverlayGradientMath.Angle(gradient, 200, 200), 0), "0° points up");
    OverlayGradientMath.Handles(gradient, 200, 200, out var start, out var end, out var across);
    Check(Near(Vector2Dot(end - start, across - start), 0), "the width handle stays square to the end handle");
  }

  private static void FlipRotateAndKindKeepTheGradientUsable()
  {
    OverlayColorSettings color = OverlayColorSettings.Custom("#000000");
    OverlayGradientEdits.AddStop(color, 1, "#BA712B");
    color.Gradient.Stops[1].Position = .8f;
    color.Gradient.Stops[0].PositionFormula = "{progress-current}";
    OverlayGradientEdits.Flip(color);
    Check(Near(color.Gradient.Stops[1].Position, .2f), "flipping mirrors positions");
    Check(color.Gradient.Stops[0].Position == 0, "a stop following a value stays with it");
    color.Gradient.AngleFormula = "{bpm}";
    OverlayGradientEdits.Rotate(color.Gradient, 100, 100);
    Check(color.Gradient.AngleFormula == "" && Near(OverlayGradientMath.Angle(color.Gradient, 100, 100), 180), "rotate turns a quarter");
    OverlayGradientEdits.SetKind(color.Gradient, OverlayGradientKind.Angular);
    Check(color.Gradient.Kind == OverlayGradientKind.Angular && Near(color.Gradient.StartX, .5f), "a new kind starts from its own handles");
  }

  private static void SwitchingToTheHighlightKeepsAFade()
  {
    OverlayColorSettings color = OverlayColorSettings.Custom("#204060");
    OverlayGradientEdits.AddStop(color, 1, "#BA712B");
    OverlayGradientEdits.AddStop(color, .5f, "#BA712B");
    foreach (OverlayGradientStop stop in color.Gradient.Stops)
      stop.Alpha = 1;
    OverlayGradientEdits.SetHighlight(color, true, "#BA712B");
    List<int> order = OverlayGradientEdits.Order(color.Gradient);
    Check(color.Highlight, "the whole gradient follows the highlight");
    Check(
      color.Gradient.Stops[order[0]].Alpha == 1
        && Near(color.Gradient.Stops[order[1]].Alpha, .7f)
        && Near(color.Gradient.Stops[order[2]].Alpha, .4f),
      "equal opacities would vanish, so they fade"
    );
    OverlayGradientEdits.SetHighlight(color, false, "#BA712B");
    Check(!color.Highlight && color.Gradient.Stops.TrueForAll(stop => stop.Hex == "#BA712B"), "custom starts from the highlight");
    Check(Near(color.Gradient.Stops[order[2]].Alpha, .4f), "and keeps the opacities");
  }

  private static void StopsSnapToQuartersAndEachOther()
  {
    OverlayColorSettings color = OverlayColorSettings.Custom("#000000");
    OverlayGradientEdits.AddStop(color, 1, "#BA712B");
    OverlayGradientEdits.AddStop(color, .6f, "#BA712B");
    Check(OverlayGradientEdits.Snap(color.Gradient, 2, .51f) == .5f, "a quarter pulls");
    Check(OverlayGradientEdits.Snap(color.Gradient, 1, .61f) == .6f, "another stop pulls");
    Check(Near(OverlayGradientEdits.Snap(color.Gradient, 2, .4f), .4f), "far from both, it stays");
  }

  private static void GradientsSurviveTheSettingsFileAndOldVersions()
  {
    OverlayLayoutSettings original = new()
    {
      Id = "shape:1",
      TemplateId = OverlayElementKinds.Shape,
      Customized = true,
      Shape = OverlayShapeGeometry.Create(OverlayShapeKind.Square),
    };
    OverlayColorSettings fill = OverlayColorSettings.Custom("#BA712B");
    OverlayGradientEdits.AddStop(fill, 1, "#BA712B");
    fill.Gradient.Kind = OverlayGradientKind.Angular;
    fill.Gradient.AngleFormula = "{bpm}";
    fill.Gradient.Stops[1].PositionFormula = "{progress-current}";
    fill.Gradient.Stops[1].AlphaFormula = "{accuracy}";
    original.Shape.Fill = fill;
    XmlSerializer serializer = new(typeof(OverlayLayoutSettings));
    using StringWriter writer = new();
    serializer.Serialize(writer, original);
    string xml = writer.ToString();
    using StringReader reader = new(xml);
    OverlayLayoutSettings loaded = (OverlayLayoutSettings)serializer.Deserialize(reader);
    Check(loaded.SameAs(original) && loaded.Shape.Fill.IsGradient, "gradients round-trip through the settings file");
    Check(
      loaded.Shape.Fill.Gradient.AngleFormula == "{bpm}" && loaded.Shape.Fill.Gradient.Stops[1].PositionFormula == "{progress-current}",
      "their formulas load back"
    );
    Check(loaded.Shape.Stroke.Gradient == null, "solid colors stay solid");
    Check(!xml.Contains("<IsGradient"), "only saved fields are written");
    Check(fill.Hex == "#BA712B" && fill.Alpha == 1, "the first color stays in the solid fields for older versions");
    OverlayColorSettings other = fill.Clone();
    other.Gradient.Stops[1].Hex = "#000000";
    Check(!other.SameAs(fill), "a changed stop color is a change");
    other = fill.Clone();
    other.Highlight = true;
    fill.Highlight = true;
    other.Gradient.Stops[1].Hex = "#000000";
    Check(other.SameAs(fill), "following the highlight, stop colors don't matter");
  }

  private static float Vector2Dot(System.Numerics.Vector2 a, System.Numerics.Vector2 b) => System.Numerics.Vector2.Dot(a, b);

  private static bool Near(float a, float b) => Math.Abs(a - b) < .01f;

  private static void Check(bool condition, string message)
  {
    if (!condition)
      throw new InvalidOperationException(message);
  }
}
