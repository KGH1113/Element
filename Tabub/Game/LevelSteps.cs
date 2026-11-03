using System;
using System.Collections.Generic;
using Tabub.Planning;

namespace Tabub.Game;

/// <summary>The level's tiles after where play started, as keys and gaps in groups.</summary>
internal static class LevelSteps
{
  /// <param name="baseBpm">The level's BPM without the song's pitch; a tile's BPM is this times its speed.</param>
  public static void Read(IReadOnlyList<scrFloor> floors, int start, double baseBpm, List<TabubStep> steps)
  {
    steps.Clear();
    for (int index = start + 1; index < floors.Count; index++)
    {
      scrFloor floor = floors[index];
      if (floor == null)
      {
        steps.Add(new TabubStep(0, 0));
        continue;
      }
      // A midspin is passed without a key; a tile that needs several taps takes that many.
      int keys = floor.midSpin ? 0 : Math.Max(1, floor.tapsNeeded);
      scrFloor next = index + 1 < floors.Count ? floors[index + 1] : null;
      double groups = next == null ? 0 : TabubUnits.FromSeconds(next.entryTime - floor.entryTime, baseBpm * floor.speed);
      steps.Add(new TabubStep(keys, groups));
    }
  }
}
