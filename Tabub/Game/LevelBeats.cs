using System;
using System.Collections.Generic;
using Tabub.Planning;

namespace Tabub.Game;

/// <summary>The chart's hits from the level's tiles, as beats from the first hit.</summary>
internal static class LevelBeats
{
  /// <param name="beats">Each hit's beat; a tile that needs several taps adds its beat once per tap.</param>
  /// <param name="hitFloors">The tile of each hit.</param>
  public static void Read(IReadOnlyList<scrFloor> floors, double referenceBpm, List<double> beats, List<int> hitFloors)
  {
    beats.Clear();
    hitFloors.Clear();
    double origin = double.NaN;
    // The first tile is where the planet starts; every later tile is a hit,
    // except a midspin, which the planet passes without one.
    for (int index = 1; index < floors.Count; index++)
    {
      scrFloor floor = floors[index];
      if (floor == null || floor.midSpin)
        continue;
      if (double.IsNaN(origin))
        origin = floor.entryTime;
      double beat = TabubBeats.FromSeconds(floor.entryTime - origin, referenceBpm);
      for (int tap = 0; tap < Math.Max(1, floor.tapsNeeded); tap++)
      {
        beats.Add(beat);
        hitFloors.Add(index);
      }
    }
  }
}
