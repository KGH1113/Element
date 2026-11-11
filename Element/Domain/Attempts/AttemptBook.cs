using System.Collections.Generic;

namespace Element.Domain.Attempts;

/// <summary>Every map's attempts, counted per start tile and speed.</summary>
internal sealed class AttemptBook
{
  private readonly Dictionary<MapId, MapAttempts> _maps = new();

  public IReadOnlyDictionary<MapId, MapAttempts> Maps => _maps;

  public AttemptCounts Read(MapId map, AttemptKey key) =>
    _maps.TryGetValue(map, out MapAttempts attempts) ? attempts.Counts(key) : default;

  public AttemptCounts Add(MapId map, AttemptKey key)
  {
    MapAttempts attempts = GetOrCreate(map);
    attempts.Change(key, 1);
    return attempts.Counts(key);
  }

  public AttemptCounts Remove(MapId map, AttemptKey key)
  {
    if (!_maps.TryGetValue(map, out MapAttempts attempts))
      return default;
    attempts.Change(key, -1);
    if (attempts.Total > 0)
      return attempts.Counts(key);
    _maps.Remove(map);
    return default;
  }

  /// <summary>Puts back a count read from disk.</summary>
  public void Restore(MapId map, AttemptKey key, int count)
  {
    if (count > 0)
      GetOrCreate(map).Change(key, count);
  }

  private MapAttempts GetOrCreate(MapId map)
  {
    if (!_maps.TryGetValue(map, out MapAttempts attempts))
      _maps[map] = attempts = new MapAttempts();
    return attempts;
  }
}
