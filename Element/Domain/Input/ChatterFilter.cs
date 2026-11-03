using System;
using System.Threading;

namespace Element.Domain.Input;

/// <summary>
/// Drops key chatter the way KeyboardChatterBlocker does: a press of a key that
/// comes no later than the threshold after that key's last accepted press is
/// dropped. A dropped press doesn't move that time, so a bouncing key is
/// caught until it settles. Releases are never filtered, and keys on the
/// ignored list always pass.
/// </summary>
/// <remarks>
/// One thread feeds each instance (SkyHook's native thread or Unity's main
/// thread). Nothing here allocates, locks, logs or touches Unity, so it is safe
/// on the input hook and costs a few nanoseconds per press.
/// </remarks>
internal sealed class ChatterFilter
{
  public const int MinimumWindowMs = 10;
  public const int MaximumWindowMs = 500;
  public const int DefaultWindowMs = 100;

  /// <summary>
  /// Unity input asks for presses more than once a frame; a press this close
  /// to the last accepted one is that same press asked again.
  /// </summary>
  public const int SameFrameMs = 5;

  // Windows virtual keys, macOS HID usages and Unity key codes all fit.
  public const int KeyCount = 1024;
  private static readonly bool[] NoKeys = new bool[KeyCount];

  private readonly long[] _acceptedAt = new long[KeyCount];
  private readonly bool _sameFrame;
  private bool[] _ignored = NoKeys;
  private int _windowMs = -1;
  private long _blocked;

  /// <param name="sameFrame">Accept presses within <see cref="SameFrameMs"/>, for Unity input.</param>
  public ChatterFilter(bool sameFrame) => _sameFrame = sameFrame;

  /// <summary>Presses dropped since the mod loaded.</summary>
  public long Blocked => Interlocked.Read(ref _blocked);

  public bool Enabled => Volatile.Read(ref _windowMs) >= 0;

  public static int ClampWindow(int milliseconds) => Math.Max(MinimumWindowMs, Math.Min(MaximumWindowMs, milliseconds));

  /// <summary>
  /// Any thread. <paramref name="ignored"/> is indexed by key and is not
  /// copied; pass a new array to change it. Press times are kept.
  /// </summary>
  public void Configure(bool enabled, int windowMs, bool[] ignored)
  {
    Volatile.Write(ref _ignored, ignored != null && ignored.Length == KeyCount ? ignored : NoKeys);
    Volatile.Write(ref _windowMs, enabled ? ClampWindow(windowMs) : -1);
  }

  /// <summary>Feeding thread only. Returns false when the press is chatter.</summary>
  public bool AcceptPress(int key, long nowMs)
  {
    int window = Volatile.Read(ref _windowMs);
    if (window < 0 || (uint)key >= KeyCount || Volatile.Read(ref _ignored)[key])
      return true;
    long since = nowMs - _acceptedAt[key];
    if (since > window || (_sameFrame && since <= SameFrameMs))
    {
      _acceptedAt[key] = nowMs;
      return true;
    }
    Interlocked.Increment(ref _blocked);
    return false;
  }
}
