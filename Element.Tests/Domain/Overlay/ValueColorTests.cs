using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml.Serialization;
using Element.Configuration;
using Element.Configuration.Overlay;
using Element.Domain.Overlay.Gradient;
using Element.Domain.Overlay.Text;
using Element.Domain.Overlay.ValueColors;
using Element.Domain.Overlay.Values;
using Element.Presentation.Overlay.Text;
using Element.Tests.Support;
using UnityEngine;
using static Element.Tests.Support.Assert;

namespace Element.Tests.Domain.Overlay;

internal static class ValueColorTests
{
  private static readonly OverlayRgba Highlight = new(.73f, .44f, .17f, 1);

  [Suite("colors that follow a value: blending, steps, defaults, edits, saving and drawing")]
  public static void Run()
  {
    SmoothBlendsBetweenStopsAndHoldsAtTheEnds();
    StepsTakeTheHighestStopReached();
    StopsInAnyOrderGiveTheSameColors();
    HighlightStopsFollowTheHighlightColor();
    ColorsBlendAsEyesSeeThem();
    DefaultsSuitTheValue();
    AddedStopsChangeNothingUntilMoved();
    RemovingKeepsTwoStops();
    StopsSnapToTidyNumbers();
    FollowingAnotherValueKeepsThePalette();
    CloneAndSameAs();
    SavedAndReadBackWhole();
    OldSettingsReadWithoutIt();
    TextKeepsEachItemsOwnColor();
    TheGameShowsTheValuesColorWithoutAllocating();
  }

  private static OverlayValueColor Ramp(OverlayValueColorMode mode, params OverlayValueColorStop[] stops)
  {
    OverlayValueColor color = new() { Value = "accuracy", Mode = mode };
    color.Stops.AddRange(stops);
    return color;
  }

  private static OverlayValueColor WhiteToBlue(OverlayValueColorMode mode) =>
    Ramp(
      mode,
      OverlayValueColorStop.Custom(0, "#FFFFFF"),
      OverlayValueColorStop.Custom(50, "#00FF00"),
      OverlayValueColorStop.Custom(100, "#0000FF")
    );

  private static bool Same(OverlayRgba a, OverlayRgba b, float tolerance = .004f) =>
    Math.Abs(a.R - b.R) <= tolerance
    && Math.Abs(a.G - b.G) <= tolerance
    && Math.Abs(a.B - b.B) <= tolerance
    && Math.Abs(a.A - b.A) <= tolerance;

  private static string Text(OverlayRgba color) => color.ToHex() + "/" + color.A.ToString("0.00");

  private static void SmoothBlendsBetweenStopsAndHoldsAtTheEnds()
  {
    OverlayValueColorRamp ramp = new(WhiteToBlue(OverlayValueColorMode.Smooth));
    Check(ramp.Count == 3 && ramp.Low == 0 && ramp.High == 100, "the ramp reads its three stops");
    Check(Same(ramp.Evaluate(-20, Highlight), OverlayRgba.Parse("#FFFFFF")), "below the lowest stop its color holds");
    Check(Same(ramp.Evaluate(140, Highlight), OverlayRgba.Parse("#0000FF")), "above the highest stop its color holds");
    Check(Same(ramp.Evaluate(50, Highlight), OverlayRgba.Parse("#00FF00")), "on a stop, its own color");
    OverlayRgba between = ramp.Evaluate(75, Highlight);
    Check(
      between.G > .2f && between.B > .2f && between.R < .2f,
      "between green and blue it is a mix of the two: " + Text(between)
    );
    OverlayRgba near = ramp.Evaluate(99, Highlight);
    Check(near.B > between.B && near.G < between.G, "closer to blue, bluer");
  }

  private static void StepsTakeTheHighestStopReached()
  {
    OverlayValueColorRamp ramp = new(WhiteToBlue(OverlayValueColorMode.Step));
    Check(Same(ramp.Evaluate(-5, Highlight), OverlayRgba.Parse("#FFFFFF")), "below every stop, the lowest one's color");
    Check(
      Same(ramp.Evaluate(49.99, Highlight), OverlayRgba.Parse("#FFFFFF")),
      "until it reaches the next stop, white holds"
    );
    Check(Same(ramp.Evaluate(50, Highlight), OverlayRgba.Parse("#00FF00")), "reaching a stop jumps to its color");
    Check(Same(ramp.Evaluate(99.9, Highlight), OverlayRgba.Parse("#00FF00")), "no blending in steps");
    Check(Same(ramp.Evaluate(100, Highlight), OverlayRgba.Parse("#0000FF")), "the top stop takes over at its value");
    // Two stops at one value: the one made later wins from there on.
    OverlayValueColorRamp edge = new(
      Ramp(
        OverlayValueColorMode.Smooth,
        OverlayValueColorStop.Custom(0, "#000000"),
        OverlayValueColorStop.Custom(50, "#FF0000"),
        OverlayValueColorStop.Custom(50, "#00FF00"),
        OverlayValueColorStop.Custom(100, "#0000FF")
      )
    );
    Check(
      Same(edge.Evaluate(50.01, Highlight).WithAlpha(1), edge.Evaluate(50, Highlight)),
      "a hard edge holds its later color"
    );
    Check(edge.Evaluate(49.9, Highlight).R > .9f, "just before a hard edge, the earlier color");
  }

  private static void StopsInAnyOrderGiveTheSameColors()
  {
    OverlayValueColor sorted = WhiteToBlue(OverlayValueColorMode.Smooth);
    OverlayValueColor shuffled = Ramp(
      OverlayValueColorMode.Smooth,
      sorted.Stops[2].Clone(),
      sorted.Stops[0].Clone(),
      sorted.Stops[1].Clone()
    );
    OverlayValueColorRamp a = new(sorted),
      b = new(shuffled);
    for (double value = -10; value <= 110; value += 7.5)
      Check(
        Same(a.Evaluate(value, Highlight), b.Evaluate(value, Highlight)),
        "stops in any order give the same color at " + value
      );
    Check(b.Source(0) == 1 && b.Source(2) == 0, "the ramp knows where each stop is saved");
    List<int> order = OverlayValueColorEdits.Order(shuffled);
    Check(order[0] == 1 && order[1] == 2 && order[2] == 0, "edits list the stops from the lowest");
  }

  private static void HighlightStopsFollowTheHighlightColor()
  {
    OverlayValueColorRamp ramp = new(
      Ramp(
        OverlayValueColorMode.Step,
        OverlayValueColorStop.Custom(0, "#FFFFFF"),
        OverlayValueColorStop.HighlightColor(100, .5f)
      )
    );
    OverlayRgba top = ramp.Evaluate(100, Highlight);
    Check(
      Same(top, Highlight.WithAlpha(.5f)),
      "a stop following the highlight takes it, with its own opacity: " + Text(top)
    );
    OverlayRgba other = ramp.Evaluate(100, new OverlayRgba(0, 0, 1, 1));
    Check(Same(other, new OverlayRgba(0, 0, 1, .5f)), "a new highlight color shows without editing the stop");
  }

  private static void ColorsBlendAsEyesSeeThem()
  {
    OverlayRgba black = new(0, 0, 0, 1),
      white = new(1, 1, 1, 1);
    OverlayRgba middle = OverlayOklab.Mix(black, white, .5f);
    // Oklab's middle gray is lighter than sRGB's 0.5 when gamma is undone; it sits near 0.39 in sRGB.
    Check(
      Math.Abs(middle.R - middle.G) < .002f && middle.R > .3f && middle.R < .5f,
      "black to white passes a true gray: " + Text(middle)
    );
    OverlayRgba red = OverlayRgba.Parse("#FF0000"),
      green = OverlayRgba.Parse("#00FF00");
    OverlayRgba mix = OverlayOklab.Mix(red, green, .5f);
    OverlayRgba plain = new(.5f, .5f, 0, 1);
    Check(mix.R + mix.G > plain.R + plain.G + .2f, "red to green stays bright instead of turning muddy: " + Text(mix));
    OverlayRgba clear = OverlayRgba.Parse("#FF0000", 0);
    OverlayRgba fading = OverlayOklab.Mix(clear, OverlayRgba.Parse("#0000FF"), .5f);
    Check(
      fading.R < .02f && Math.Abs(fading.A - .5f) < .001f,
      "a see-through stop lends none of its hue: " + Text(fading)
    );
    Check(
      Same(OverlayOklab.Mix(red, green, 0), red) && Same(OverlayOklab.Mix(red, green, 1), green),
      "the ends are the stops themselves"
    );
  }

  private static void DefaultsSuitTheValue()
  {
    OverlayColorSettings white = OverlayColorSettings.Custom("#FFFFFF");
    OverlayValueColor accuracy = OverlayValueColorDefaults.For(
      OverlayValueCatalog.Get(OverlayValueId.Accuracy),
      99.81,
      white
    );
    Check(
      accuracy.Value == "accuracy" && accuracy.Mode == OverlayValueColorMode.Step && accuracy.Stops.Count == 4,
      "X-Accuracy steps up through four stops"
    );
    OverlayValueColorStop top = accuracy.Stops[accuracy.Stops.Count - 1];
    Check(top.At == 100 && top.Highlight, "X-Accuracy reaching 100 takes the highlight color");
    Check(accuracy.Stops[0].At == 95 && accuracy.Stops[0].Hex == "#FFFFFF", "it starts from the text's own color");
    OverlayValueColorRamp ramp = new(accuracy);
    Check(
      Same(ramp.Evaluate(99.81, Highlight), OverlayRgba.Parse(OverlayValueColorDefaults.Sky)),
      "99.81 shows sky blue"
    );

    OverlayValueColor progress = OverlayValueColorDefaults.For(
      OverlayValueCatalog.Get(OverlayValueId.ProgressCurrent),
      48,
      OverlayColorSettings.HighlightColor()
    );
    Check(
      progress.Mode == OverlayValueColorMode.Smooth && progress.Stops[0].At == 0 && progress.Stops[1].At == 100,
      "progress blends from 0 to 100"
    );
    Check(
      progress.Stops[0].Hex == "#FFFFFF" && !progress.Stops[0].Highlight,
      "a text in the highlight color starts from white, so the change shows"
    );

    OverlayValueColor combo = OverlayValueColorDefaults.For(OverlayValueCatalog.Get(OverlayValueId.Combo), 120, white);
    Check(combo.Stops[0].At == 0 && combo.Stops[1].At == 200, "combo 120 blends over 0–200: " + combo.Stops[1].At);
    OverlayValueColor none = OverlayValueColorDefaults.For(null, double.NaN, white);
    Check(none.Value == "" && none.Stops[1].At == 100, "with no value yet it spans 0–100");
    OverlayValueColorDefaults.Fit(-30, out float low, out float high);
    Check(low == -50 && high == 0, "a value below zero gets a range below zero: " + low);
    Check(
      OverlayValueColorDefaults.NiceCeiling(270) == 300 && OverlayValueColorDefaults.NiceCeiling(9) == 10,
      "ranges end on round numbers"
    );
  }

  private static void AddedStopsChangeNothingUntilMoved()
  {
    OverlayValueColor smooth = WhiteToBlue(OverlayValueColorMode.Smooth);
    OverlayRgba before = new OverlayValueColorRamp(smooth).Evaluate(25, Highlight);
    int added = OverlayValueColorEdits.AddStop(smooth, 25, Highlight);
    OverlayValueColorRamp after = new(smooth);
    Check(added == 3 && smooth.Stops.Count == 4, "a stop is added at the end of the list");
    Check(Same(after.Evaluate(25, Highlight), before, .01f), "it takes the color the band had there");
    Check(
      Same(
        after.Evaluate(12, Highlight),
        new OverlayValueColorRamp(WhiteToBlue(OverlayValueColorMode.Smooth)).Evaluate(12, Highlight),
        .03f
      ),
      "the band around it barely changes"
    );

    OverlayValueColor steps = Ramp(
      OverlayValueColorMode.Step,
      OverlayValueColorStop.Custom(0, "#FFFFFF"),
      OverlayValueColorStop.HighlightColor(100)
    );
    OverlayValueColorEdits.AddStop(steps, 120, Highlight);
    Check(
      steps.Stops[2].Highlight && steps.Stops[2].At == 120,
      "past the top in steps it takes the top stop whole, highlight and all"
    );
    for (int i = 0; i < 10; i++)
      OverlayValueColorEdits.AddStop(steps, i * 3, Highlight);
    Check(steps.Stops.Count == OverlayValueColor.MaximumStops, "a color holds at most eight stops");
  }

  private static void RemovingKeepsTwoStops()
  {
    OverlayValueColor color = WhiteToBlue(OverlayValueColorMode.Smooth);
    Check(OverlayValueColorEdits.RemoveStop(color, 1) && color.Stops.Count == 2, "a third stop can go");
    Check(!OverlayValueColorEdits.RemoveStop(color, 0) && color.Stops.Count == 2, "the last two stay");
  }

  private static void StopsSnapToTidyNumbers()
  {
    Check(Math.Abs(OverlayValueColorEdits.Step(95, 100) - .1f) < 1e-6f, "95–100 snaps to tenths");
    Check(OverlayValueColorEdits.Step(0, 200) == 5, "0–200 snaps to fives");
    Check(
      OverlayValueColorEdits.Snap(98.3149f, 95, 100) == 98.3f,
      "no float dust: " + OverlayValueColorEdits.Snap(98.3149f, 95, 100)
    );
    Check(OverlayValueColorEdits.Snap(123.4f, 0, 200) == 125, "to the nearest five");
    OverlayValueColorEdits.Range(
      Ramp(OverlayValueColorMode.Step, OverlayValueColorStop.Custom(10, "#FFFFFF")),
      out float low,
      out float high
    );
    Check(low < 10 && high > 10, "one stop alone still spans a band");
  }

  private static void FollowingAnotherValueKeepsThePalette()
  {
    OverlayColorSettings white = OverlayColorSettings.Custom("#FFFFFF");
    OverlayValueColor accuracy = OverlayValueColorDefaults.For(
      OverlayValueCatalog.Get(OverlayValueId.Accuracy),
      99,
      white
    );
    accuracy.Stops[1].Hex = "#123456";
    OverlayValueColor combo = OverlayValueColorEdits.Follow(
      accuracy,
      OverlayValueCatalog.Get(OverlayValueId.Combo),
      120,
      white
    );
    Check(
      combo.Value == "combo" && combo.Stops.Count == 4 && combo.Stops[1].Hex == "#123456",
      "following combo keeps the colors"
    );
    Check(
      combo.Stops[0].At == 0 && combo.Stops[3].At == 200 && combo.Stops[1].At == 120,
      "spread the same way over combo's range"
    );
    Check(accuracy.Value == "accuracy", "the color it came from is left as it was");
    OverlayValueColor fresh = OverlayValueColorEdits.Follow(
      OverlayValueColorDefaults.For(null, double.NaN, white),
      OverlayValueCatalog.Get(OverlayValueId.Accuracy),
      99,
      white
    );
    Check(
      fresh.Stops.Count == 4 && fresh.Mode == OverlayValueColorMode.Step,
      "a color that followed nothing yet starts as the value's own look"
    );
  }

  private static void CloneAndSameAs()
  {
    OverlayColorSettings color = OverlayColorSettings.Custom("#FFFFFF");
    color.ValueColor = WhiteToBlue(OverlayValueColorMode.Step);
    OverlayColorSettings copy = color.Clone();
    Check(
      copy.SameAs(color) && copy.ValueColor != color.ValueColor,
      "a copy follows the value the same way, on its own stops"
    );
    copy.ValueColor.Stops[1].At = 60;
    Check(!copy.SameAs(color) && color.ValueColor.Stops[1].At == 50, "changing the copy leaves the original");
    copy = color.Clone();
    copy.ValueColor.Mode = OverlayValueColorMode.Smooth;
    Check(!copy.SameAs(color), "smooth and steps differ");
    copy = color.Clone();
    copy.ValueColor = null;
    Check(!copy.SameAs(color) && !color.SameAs(copy), "following a value differs from not");
    OverlayValueColorStop highlight = OverlayValueColorStop.HighlightColor(10);
    OverlayValueColorStop other = highlight.Clone();
    other.Hex = "#000000";
    Check(highlight.SameAs(other), "a stop following the highlight ignores its unused HEX");
    color.Gradient = new OverlayGradient();
    color.Gradient.Stops.Add(new OverlayGradientStop());
    color.Gradient.Stops.Add(new OverlayGradientStop { Position = 1, Hex = "#000000" });
    copy = color.Clone();
    copy.ValueColor.Value = "combo";
    Check(!copy.SameAs(color), "the followed value counts even under a gradient");
  }

  private static void SavedAndReadBackWhole()
  {
    OverlayLayoutSettings layout = new() { Id = "value-color", Text = new OverlayTextSettings() };
    OverlayColorSettings color = OverlayColorSettings.HighlightColor(.8f);
    color.ValueColor = OverlayValueColorDefaults.For(OverlayValueCatalog.Get(OverlayValueId.Accuracy), 99, color);
    color.ValueColor.Stops[2].Alpha = .25f;
    layout.Text.Runs.Add(OverlayTextRun.ValueRun("accuracy", color));
    ElementSettings settings = new();
    settings.OverlayLayouts.Add(layout);
    ElementSettings read = SettingsXml.Clone(settings);
    OverlayColorSettings back = read.OverlayLayouts.Find(item => item.Id == "value-color").Text.Runs[0].Color;
    Check(back.SameAs(color), "a color that follows a value survives the settings file");
    Check(
      back.ValueColor.Stops[2].Alpha == .25f && back.ValueColor.Stops[3].Highlight,
      "with each stop's opacity and highlight"
    );
    Check(
      layout.Clone().SameAs(layout) && layout.Clone().Text.Runs[0].Color.ValueColor != color.ValueColor,
      "a layout copy has its own"
    );
  }

  private static void OldSettingsReadWithoutIt()
  {
    const string old =
      "<OverlayColorSettings><Highlight>false</Highlight><Hex>#ABCDEF</Hex><Alpha>0.5</Alpha>"
      + "<AlphaFormula /></OverlayColorSettings>";
    using MemoryStream stream = new(Encoding.UTF8.GetBytes(old));
    OverlayColorSettings read = SettingsXml.Read<OverlayColorSettings>(stream);
    Check(
      read != null && read.ValueColor == null && !read.FollowsValue && read.Hex == "#ABCDEF",
      "a color saved before reads as one that follows nothing"
    );
    using StringWriter writer = new();
    new XmlSerializer(typeof(OverlayColorSettings)).Serialize(writer, read);
    Check(!writer.ToString().Contains("ValueColor"), "and is written without it");
  }

  private static void TextKeepsEachItemsOwnColor()
  {
    List<OverlayTextRun> runs = new()
    {
      OverlayTextRun.Literal("Acc ", OverlayColorSettings.Custom("#FFFFFF")),
      OverlayTextRun.ValueRun("accuracy", OverlayColorSettings.HighlightColor()),
    };
    OverlayTextDocument document = OverlayTextDocument.FromRuns(runs);
    OverlayValueColor follows = OverlayValueColorDefaults.For(
      OverlayValueCatalog.Get(OverlayValueId.Accuracy),
      99,
      null
    );
    Check(document.FollowsValue(0, document.Count) == false, "a text starts following nothing");
    document.SetValueColor(0, 2, follows);
    Check(document.FollowsValue(0, document.Count) == null, "part of it follows the value");
    document.SetValueColor(0, document.Count, follows);
    Check(document.FollowsValue(0, document.Count) == true, "all of it follows the value");
    Check(
      document[0].Color.Hex == "#FFFFFF" && document[document.Count - 1].Color.Highlight,
      "each part keeps its own color underneath"
    );
    List<OverlayTextRun> saved = document.ToRuns();
    Check(
      saved.Count == 2 && saved[0].Color.FollowsValue && saved[1].Color.FollowsValue,
      "saved runs follow the value too"
    );
    document.SetValueColor(0, document.Count, null);
    Check(
      document.FollowsValue(0, document.Count) == false && document[4].Color.Highlight,
      "turning it off brings the colors back"
    );
    List<OverlayValueId> values = new();
    OverlayTextDocument
      .FromRuns(
        new[]
        {
          OverlayTextRun.ValueRun("{combo}*2+{miss}", new OverlayColorSettings()),
          OverlayTextRun.ValueRun("combo", new OverlayColorSettings()),
        }
      )
      .ValuesIn(0, 2, values);
    Check(
      values.Count == 2 && values[0] == OverlayValueId.Combo && values[1] == OverlayValueId.Miss,
      "a text lists each value it shows once"
    );
  }

  private static void TheGameShowsTheValuesColorWithoutAllocating()
  {
    // The text's own color follows the highlight here: reading a HEX needs Unity.
    OverlayColorSettings color = OverlayColorSettings.HighlightColor(.9f);
    color.ValueColor = OverlayValueColorDefaults.For(OverlayValueCatalog.Get(OverlayValueId.Accuracy), 99, color);
    List<OverlayTextRun> runs = new() { OverlayTextRun.ValueRun("accuracy", color) };
    List<OverlayCompiledRun> compiled = new();
    OverlayValueMask mask = OverlayTextComposer.Compile(runs, compiled);
    Check(
      mask.Contains(OverlayValueId.Accuracy) && compiled[0].ValueColor != null,
      "the text refreshes when the followed value changes"
    );
    OverlayValueColorRamp ramp = compiled[0].ValueColor;
    var follows = compiled[0].ValueColorSource;
    OverlayFixtures.Numbers numbers = new();
    Color orange = new(1, .5f, 0);
    Color none = OverlayTextComposer.Resolve(color, orange, null, numbers, ramp, follows);
    Check(none == new Color(1, .5f, 0, .9f), "with no data yet, the text's own color shows: " + none);
    numbers[OverlayValueId.Accuracy] = 100;
    Color top = OverlayTextComposer.Resolve(color, orange, null, numbers, ramp, follows);
    Check(
      Mathf.Abs(top.r - 1) < .001f && Mathf.Abs(top.g - .5f) < .001f && top.b < .001f,
      "at 100 the highlight color: " + top
    );
    numbers[OverlayValueId.Accuracy] = 98.5;
    Color green = OverlayTextComposer.Resolve(color, orange, null, numbers, ramp, follows);
    Check(
      Same(new OverlayRgba(green.r, green.g, green.b, 1), OverlayRgba.Parse(OverlayValueColorDefaults.Green)),
      "98.5 shows green"
    );
    ramp.Load(
      new OverlayValueColor
      {
        Mode = OverlayValueColorMode.Smooth,
        Stops = { OverlayValueColorStop.Custom(0, "#FFFFFF"), OverlayValueColorStop.Custom(200, "#0000FF") },
      }
    );
    OverlayTextComposer.Resolve(color, Color.white, null, numbers, ramp, follows);
    long before = GC.GetAllocatedBytesForCurrentThread();
    for (int i = 0; i < 1000; i++)
    {
      numbers[OverlayValueId.Accuracy] = i % 200;
      OverlayTextComposer.Resolve(color, Color.white, null, numbers, ramp, follows);
    }
    long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
    Check(allocated == 0, "working out the color allocated " + allocated + " bytes");
  }
}
