using System;
using System.Collections.Generic;
using System.Linq;
using Tabub.Planning;

namespace Element.Tests;

// Tabub's rules with the examples they were written from: beats at 2021 BPM,
// four beats per hand, two hits within a beat pressed together.
internal static class TabubTests
{
  private static readonly TabubSettings Settings = new();

  public static void Run()
  {
    SpeedChangesCountTheSameBeats();
    RightAngleFiveIsTwoTwoOne();
    FastEightIsFourPairs();
    HandsSwitchEveryFourBeats();
    BusyStretchesSwitchHandsSooner();
    TapsOnOneTileArePressedTogether();
  }

  private static void SpeedChangesCountTheSameBeats()
  {
    Check(Near(TabubBeats.FromTurn(180, 2021, 2021), 1), "180° at 2021 BPM is one beat");
    Check(Near(TabubBeats.FromTurn(90, 2021, 2021), .5), "90° at 2021 BPM is half a beat");
    Check(Near(TabubBeats.FromTurn(45, 1010.5, 2021), .5), "45° at half the speed is also half a beat");
    double seconds = 45.0 / 180 * (60 / 1010.5);
    Check(Near(TabubBeats.FromSeconds(seconds, 2021), .5), "a tile's time gives the same beats as its turn");
  }

  private static void RightAngleFiveIsTwoTwoOne()
  {
    TabubPlan plan = TabubPlanner.Plan(new[] { 0, .5, 1, 2, 3.0 }, Settings);
    TabubSection section = plan.Sections.Single();
    Check(section.Hand == Hand.Left && section.HitCount == 5, "the first four beats are the left hand's");
    Check(section.Pattern == "2 + 2 + 1", "right angles: 2 + 2 + 1, got " + section.Pattern);
    Check(
      section.Actions.Select(action => action.Beat).SequenceEqual(new[] { .25, 1.5, 3 }),
      "pairs land between their hits: 0.25, 1.5, then 3"
    );
  }

  private static void FastEightIsFourPairs()
  {
    double[] eight = Enumerable.Range(0, 8).Select(i => i * .5).ToArray();
    TabubSection section = TabubPlanner.Plan(eight, Settings).Sections.Single();
    Check(section.Pattern == "2 + 2 + 2 + 2", "fast eight: 2 + 2 + 2 + 2, got " + section.Pattern);
    Check(section.Actions.Select(action => action.Beat).SequenceEqual(new[] { .25, 1.25, 2.25, 3.25 }), "at 0.25, 1.25, 2.25 and 3.25");
  }

  private static void HandsSwitchEveryFourBeats()
  {
    List<double> beats = new() { 0, .5, 1, 2, 3 };
    beats.AddRange(new[] { 3.9999999, 4.5, 5, 6, 7 });
    beats.AddRange(new[] { 8.0, 8.5 });
    TabubPlan plan = TabubPlanner.Plan(beats, Settings);
    Check(plan.Sections.Count == 3, "three stretches");
    Check(
      plan.Sections.Select(section => section.Hand).SequenceEqual(new[] { Hand.Left, Hand.Right, Hand.Left }),
      "left, right, left"
    );
    Check(plan.SectionOf(5) == 1, "a hit on the boundary belongs to the next hand, even a hair early from rounding");
    Check(plan.Sections[1].Pattern == "2 + 2 + 1", "the right hand plays the same right angles");

    TabubPlan rest = TabubPlanner.Plan(new[] { 0, 8.0 }, Settings);
    Check(
      rest.Sections.Select(section => section.Hand).SequenceEqual(new[] { Hand.Left, Hand.Left }),
      "an empty stretch still takes its turn, so beat 8 is the left hand's again"
    );
  }

  private static void BusyStretchesSwitchHandsSooner()
  {
    double[] twelve = Enumerable.Range(0, 12).Select(i => i / 3.0).ToArray();
    TabubPlan plan = TabubPlanner.Plan(twelve, Settings);
    Check(plan.HandInterval == 2 && !plan.TooBusy, "twelve inputs in four beats switch hands every two beats");
    Check(plan.Sections.All(section => section.HitCount <= Settings.MaxInputsPerHand), "no hand plays more than eight");

    double[] wall = Enumerable.Repeat(0.0, 20).ToArray();
    TabubPlan tooBusy = TabubPlanner.Plan(wall, Settings);
    Check(tooBusy.TooBusy && tooBusy.HandInterval >= Settings.MinimumHandInterval, "a chart too busy even then says so");
  }

  private static void TapsOnOneTileArePressedTogether()
  {
    TabubSection section = TabubPlanner.Plan(new[] { 0, 0, 2.0 }, Settings).Sections.Single();
    Check(section.Pattern == "2 + 1" && section.Actions[0].Beat == 0, "a two-tap tile is one press of two keys");
  }

  private static bool Near(double a, double b) => Math.Abs(a - b) < 1e-9;

  private static void Check(bool value, string message)
  {
    if (!value)
      throw new InvalidOperationException(message);
  }
}
