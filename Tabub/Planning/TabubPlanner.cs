using System;
using System.Collections.Generic;
using System.Text;

namespace Tabub.Planning;

public enum Hand
{
  Left,
  Right,
}

/// <summary>How a chart is split between the hands. Beats are counted at <see cref="ReferenceBpm"/>.</summary>
public sealed class TabubSettings
{
  /// <summary>One beat is a half turn at this BPM; speed changes are converted to it.</summary>
  public double ReferenceBpm = 2021;

  /// <summary>How many beats one hand takes before the other hand does.</summary>
  public double HandInterval = 4;

  /// <summary>The most inputs one hand plays in its stretch; a busier chart switches hands sooner.</summary>
  public int MaxInputsPerHand = 8;

  /// <summary>Two hits this close, in beats, may land half a gap early or late, so one press plays both.</summary>
  public double Tolerance = 0.5;

  /// <summary>Hands never switch faster than this, in beats, even when a stretch stays too busy.</summary>
  public double MinimumHandInterval = 0.5;
}

/// <summary>One press of one hand: one key, or two keys together for two close hits.</summary>
public readonly struct TabubAction
{
  public TabubAction(double beat, int inputs, int firstHit)
  {
    Beat = beat;
    Inputs = inputs;
    FirstHit = firstHit;
  }

  /// <summary>When to press, in beats; between the two hits for a pair.</summary>
  public double Beat { get; }

  /// <summary>Keys pressed together: 1 or 2.</summary>
  public int Inputs { get; }

  public int FirstHit { get; }
}

/// <summary>The stretch one hand plays: its hits, grouped into presses.</summary>
public sealed class TabubSection
{
  internal TabubSection(int index, Hand hand, int firstHit, int hitCount, List<TabubAction> actions)
  {
    Index = index;
    Hand = hand;
    FirstHit = firstHit;
    HitCount = hitCount;
    Actions = actions;
    Pattern = Describe(actions);
  }

  /// <summary>Which stretch of the chart, counting empty ones; even stretches are the left hand's.</summary>
  public int Index { get; }

  public Hand Hand { get; }
  public int FirstHit { get; }

  /// <summary>Inputs in the stretch: one key for each hit.</summary>
  public int HitCount { get; }

  public IReadOnlyList<TabubAction> Actions { get; }

  /// <summary>Keys per press, such as "2 + 2 + 1".</summary>
  public string Pattern { get; }

  private static string Describe(List<TabubAction> actions)
  {
    StringBuilder builder = new();
    foreach (TabubAction action in actions)
    {
      if (builder.Length > 0)
        builder.Append(" + ");
      builder.Append(action.Inputs);
    }
    return builder.ToString();
  }
}

public sealed class TabubPlan
{
  private readonly int[] _sectionOfHit;

  internal TabubPlan(List<TabubSection> sections, int[] sectionOfHit, double handInterval, bool tooBusy)
  {
    Sections = sections;
    _sectionOfHit = sectionOfHit;
    HandInterval = handInterval;
    TooBusy = tooBusy;
  }

  /// <summary>Stretches that have hits, in order.</summary>
  public IReadOnlyList<TabubSection> Sections { get; }

  /// <summary>The beats per hand actually used: the setting, or shorter when a stretch was too busy.</summary>
  public double HandInterval { get; }

  /// <summary>Some stretch has more inputs than one hand plays, even at the shortest switch.</summary>
  public bool TooBusy { get; }

  public int HitCount => _sectionOfHit.Length;

  /// <summary>The stretch that plays <paramref name="hit"/>, as an index into <see cref="Sections"/>.</summary>
  public int SectionOf(int hit) => hit >= 0 && hit < _sectionOfHit.Length ? _sectionOfHit[hit] : -1;
}

/// <summary>
/// Splits a chart between the hands: every stretch of beats goes to one hand,
/// alternating left and right, and inside a stretch two close hits become one
/// press of two keys. Choosing actual keys for each press is left to the player.
/// </summary>
public static class TabubPlanner
{
  // Beats come from seconds; this absorbs rounding so a hit on a boundary
  // belongs to the stretch it starts.
  private const double Epsilon = 1e-6;

  /// <param name="beats">Each hit's beat from the first hit, in order. Hits on one tile that needs several taps repeat their beat.</param>
  public static TabubPlan Plan(IReadOnlyList<double> beats, TabubSettings settings)
  {
    settings ??= new TabubSettings();
    double interval = Math.Max(settings.MinimumHandInterval, settings.HandInterval);
    while (true)
    {
      TabubPlan plan = Split(beats, settings, interval);
      double shorter = interval / 2;
      if (!plan.TooBusy || shorter < settings.MinimumHandInterval - Epsilon)
        return plan;
      // One hand can't play a stretch this busy: switch hands twice as often.
      interval = shorter;
    }
  }

  private static TabubPlan Split(IReadOnlyList<double> beats, TabubSettings settings, double interval)
  {
    bool tooBusy = false;
    List<TabubSection> sections = new();
    int[] sectionOfHit = new int[beats.Count];
    int first = 0;
    while (first < beats.Count)
    {
      int index = StretchOf(beats[first], interval);
      int end = first;
      while (end < beats.Count && StretchOf(beats[end], interval) == index)
        end++;
      int count = end - first;
      if (count > settings.MaxInputsPerHand)
        tooBusy = true;
      List<TabubAction> actions = Group(beats, first, end, settings.Tolerance);
      Hand hand = index % 2 == 0 ? Hand.Left : Hand.Right;
      for (int hit = first; hit < end; hit++)
        sectionOfHit[hit] = sections.Count;
      sections.Add(new TabubSection(index, hand, first, count, actions));
      first = end;
    }
    return new TabubPlan(sections, sectionOfHit, interval, tooBusy);
  }

  // [0, interval), [interval, 2 interval), ...: a hit on a boundary starts the next stretch.
  private static int StretchOf(double beat, double interval) => (int)Math.Floor(beat / interval + Epsilon);

  // Two hits within twice the tolerance are pressed together, halfway between them.
  private static List<TabubAction> Group(IReadOnlyList<double> beats, int first, int end, double tolerance)
  {
    List<TabubAction> actions = new();
    int i = first;
    while (i < end)
    {
      if (i + 1 < end && beats[i + 1] - beats[i] <= tolerance * 2 + Epsilon)
      {
        actions.Add(new TabubAction((beats[i] + beats[i + 1]) / 2, 2, i));
        i += 2;
      }
      else
      {
        actions.Add(new TabubAction(beats[i], 1, i));
        i++;
      }
    }
    return actions;
  }
}
