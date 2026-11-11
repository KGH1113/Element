using System;
using System.Collections.Generic;

namespace Element.Domain.Attempts;

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
