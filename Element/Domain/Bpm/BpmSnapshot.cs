namespace Element.Domain.Bpm;

internal readonly struct BpmSnapshot
{
  public static BpmSnapshot Empty => default;

  public bool IsValid { get; }
  public double TileBpm { get; }
  public double CurrentBpm { get; }
  public double KeysPerSecond { get; }

  public BpmSnapshot(double tileBpm, double currentBpm, double keysPerSecond)
  {
    IsValid = true;
    TileBpm = tileBpm;
    CurrentBpm = currentBpm;
    KeysPerSecond = keysPerSecond;
  }
}
