using System;

namespace Element.Domain.Bpm;

internal static class BpmCalculator
{
  public static BpmSnapshot Calculate(
    double baseBpm,
    double pitch,
    double planetSpeed,
    double currentEntryTime,
    double? nextEntryTime
  )
  {
    double tileBpm = baseBpm * pitch * planetSpeed;
    double currentBpm = tileBpm;

    if (nextEntryTime.HasValue)
    {
      double duration = nextEntryTime.Value - currentEntryTime;
      if (duration > 0d)
        currentBpm = 60d / duration * pitch;
    }

    if (!IsFinite(tileBpm) || !IsFinite(currentBpm))
      return BpmSnapshot.Empty;

    return new BpmSnapshot(tileBpm, currentBpm, currentBpm / 60d);
  }

  private static bool IsFinite(double value)
  {
    return !double.IsNaN(value) && !double.IsInfinity(value);
  }
}
