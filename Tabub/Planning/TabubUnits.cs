using System;

namespace Tabub.Planning;

/// <summary>
/// The unit tabub counts keys in. One group is half a turn (180°), doubled
/// until the tile's BPM is under <see cref="MaxBpm"/>: below 1050 BPM a group
/// is 180°, at 1050–2100 BPM it is 360°, and so on. A group therefore always
/// lasts about the same time however fast the chart is.
/// </summary>
public static class TabubUnits
{
  public const double MaxBpm = 1050;

  /// <summary>Half turns per group at <paramref name="tileBpm"/>: 1, 2, 4, …</summary>
  public static int Threshold(double tileBpm, double maxBpm = MaxBpm)
  {
    int threshold = 1;
    if (double.IsNaN(tileBpm) || double.IsInfinity(tileBpm) || maxBpm <= 0)
      return threshold;
    while (tileBpm >= maxBpm && threshold < 1 << 24)
    {
      tileBpm /= 2;
      threshold *= 2;
    }
    return threshold;
  }

  /// <summary>
  /// How many groups a gap of <paramref name="seconds"/> between two tiles is.
  /// The planet turns 3° per second per BPM, so this is the gap's turn over
  /// 180° × threshold, read from the game's tile times so pauses count too.
  /// </summary>
  public static double FromSeconds(double seconds, double tileBpm, double maxBpm = MaxBpm)
  {
    if (!(seconds > 0) || !(tileBpm > 0) || double.IsInfinity(seconds) || double.IsInfinity(tileBpm))
      return 0;
    return seconds * tileBpm / 60.0 / Threshold(tileBpm, maxBpm);
  }

  /// <summary>How many groups a turn of <paramref name="degrees"/> is at <paramref name="tileBpm"/>.</summary>
  public static double FromTurn(double degrees, double tileBpm, double maxBpm = MaxBpm) =>
    Math.Max(0, degrees) / (180.0 * Threshold(tileBpm, maxBpm));
}
