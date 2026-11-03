using System;
using System.Collections.Generic;

namespace Tabub.Planning;

/// <summary>One tile from where play started: the keys it takes and the gap to the next tile, in groups.</summary>
public readonly struct TabubStep
{
  public TabubStep(int keys, double groups)
  {
    Keys = keys;
    Groups = groups;
  }

  /// <summary>Keys pressed on this tile: 0 for a midspin, more for a tile that needs several taps.</summary>
  public int Keys { get; }

  /// <summary>The gap to the next tile, in groups (<see cref="TabubUnits"/>).</summary>
  public double Groups { get; }
}

/// <summary>
/// How many keys fall in each group of the chart, counted from the first tile
/// after where play started. A hit belongs to the group its moment falls in;
/// one exactly on a boundary starts the next group. Groups with no hit, such
/// as a rest, count 0.
/// </summary>
public sealed class TabubChart
{
  // Tile times are seconds; this absorbs rounding so a hit on a boundary
  // starts the group it belongs to.
  private const double Epsilon = 1e-6;

  private readonly int[] _counts;
  private readonly int[] _groupOfStep;

  private TabubChart(int[] counts, int[] groupOfStep)
  {
    _counts = counts;
    _groupOfStep = groupOfStep;
  }

  /// <summary>Keys in each group, in order.</summary>
  public IReadOnlyList<int> Counts => _counts;

  public int GroupCount => _counts.Length;

  /// <summary>The group of the hit on step <paramref name="step"/>; -1 when that tile takes no key.</summary>
  public int GroupOf(int step) => step >= 0 && step < _groupOfStep.Length ? _groupOfStep[step] : -1;

  public static TabubChart Count(IReadOnlyList<TabubStep> steps)
  {
    List<int> counts = new();
    int[] groupOfStep = new int[steps.Count];
    double position = 0;
    for (int i = 0; i < steps.Count; i++)
    {
      TabubStep step = steps[i];
      if (step.Keys > 0)
      {
        int group = (int)Math.Floor(position + Epsilon);
        while (counts.Count <= group)
          counts.Add(0);
        counts[group] += step.Keys;
        groupOfStep[i] = group;
      }
      else
        groupOfStep[i] = -1;
      if (step.Groups > 0)
        position += step.Groups;
    }
    return new TabubChart(counts.ToArray(), groupOfStep);
  }
}
