using System;
using System.Threading;

namespace Element.Domain.Input;

/// <summary>
/// Drops switch bounce: a press that arrives within the window after the same
/// key was released, together with that press's own release, so the game never
/// sees a stray press or an orphan release. Measuring from the release catches
/// bounce on both the press and the release of a long hold, while fast jacks
/// on the same key still pass.
/// </summary>
/// <remarks>
/// One thread feeds each instance (SkyHook's native thread or Unity's main
/// thread). Nothing here allocates, locks, logs or touches Unity, so it is safe
/// on the input hook and costs a few nanoseconds per event.
/// </remarks>
internal sealed class ChatterFilter
{
  public const int MinimumWindowMs = 5;
  public const int MaximumWindowMs = 150;
  public const int DefaultWindowMs = 20;

  // Windows virtual keys, macOS key codes and Linux evdev codes all fit.
  private const int KeyCount = 1024;
  private const byte Released = 0;
  private const byte Held = 1;
  private const byte Swallowed = 2;

  private readonly long[] _releasedAt = new long[KeyCount];
  private readonly byte[] _state = new byte[KeyCount];
  private long _windowTicks;
  private int _generation;
  private int _seenGeneration;
  private long _blocked;

  /// <summary>Presses dropped since the mod loaded.</summary>
  public long Blocked => Interlocked.Read(ref _blocked);

  public bool Enabled => Volatile.Read(ref _windowTicks) > 0;

  public static int ClampWindow(int milliseconds) =>
    Math.Max(MinimumWindowMs, Math.Min(MaximumWindowMs, milliseconds));

  /// <summary>Any thread. The feeding thread forgets per-key state on its next event.</summary>
  public void Configure(bool enabled, int windowMs)
  {
    long ticks = enabled ? ClampWindow(windowMs) * TimeSpan.TicksPerMillisecond : 0;
    if (Interlocked.Exchange(ref _windowTicks, ticks) != ticks)
      Interlocked.Increment(ref _generation);
  }

  /// <summary>Feeding thread only. Returns false when the event is chatter.</summary>
  public bool Accept(int key, bool press, long timeTicks)
  {
    int generation = Volatile.Read(ref _generation);
    if (generation != _seenGeneration)
    {
      Array.Clear(_state, 0, KeyCount);
      Array.Clear(_releasedAt, 0, KeyCount);
      _seenGeneration = generation;
    }
    long window = Volatile.Read(ref _windowTicks);
    if (window <= 0 || (uint)key >= KeyCount)
      return true;

    if (!press)
    {
      bool pass = _state[key] != Swallowed;
      _state[key] = Released;
      // A swallowed release is still the latest physical release, so a bounce
      // right after it is caught too.
      _releasedAt[key] = timeTicks;
      return pass;
    }

    switch (_state[key])
    {
      case Held:
        // OS key repeat; the game ignores presses of a held key itself.
        return true;
      case Swallowed:
        return false;
    }
    long gap = timeTicks - _releasedAt[key];
    if (_releasedAt[key] != 0 && gap >= 0 && gap < window)
    {
      _state[key] = Swallowed;
      Interlocked.Increment(ref _blocked);
      return false;
    }
    _state[key] = Held;
    return true;
  }
}
