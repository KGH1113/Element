namespace ImplResourcePack.Domain.Attempts;

/// <summary>A play that just started.</summary>
internal readonly struct AttemptRun
{
  public AttemptRun(MapId map, AttemptKey key, bool isCounted)
  {
    Map = map;
    Key = key;
    IsCounted = isCounted;
  }

  public MapId Map { get; }
  public AttemptKey Key { get; }

  /// <summary>
  /// False with autoplay or no-fail on, or when another mod's rule skips the play:
  /// it shows the counts but adds none.
  /// </summary>
  public bool IsCounted { get; }
}
