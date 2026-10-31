using System;
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

/// <summary>One map's attempts per start tile and speed; <see cref="Total"/> is the full attempt count.</summary>
internal sealed class MapAttempts
{
  private readonly Dictionary<AttemptKey, int> _starts = new();

  public IReadOnlyDictionary<AttemptKey, int> Starts => _starts;

  public int Total { get; private set; }

  internal AttemptCounts Counts(AttemptKey key) => new(_starts.TryGetValue(key, out int count) ? count : 0, Total);

  internal void Change(AttemptKey key, int delta)
  {
    _starts.TryGetValue(key, out int count);
    int next = Math.Max(0, count + delta);
    Total += next - count;
    if (next == 0)
      _starts.Remove(key);
    else
      _starts[key] = next;
  }
}
