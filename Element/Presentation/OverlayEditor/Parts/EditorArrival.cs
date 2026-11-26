using System;
using ElementUI;
using UnityEngine;

namespace Element.Presentation.OverlayEditor.Parts;

/// <summary>
/// A short arrival that says where something came from: it runs from 0 to
/// 1, fast at first and landing softly, with no bounce. A view scales, moves
/// or fades itself by <see cref="Progress"/>, so what a slot holds can grow
/// out of that slot; nothing runs while it rests. With reduced motion it is
/// there at once.
/// </summary>
internal sealed class EditorArrival : IDisposable
{
  // How hard it is pulled to rest: critically damped, so it never overshoots.
  private const float Pull = 7;

  // Where that pull leaves it after its time, so the last frame lands on 1.
  private static readonly float Reached = 1 - (1 + Pull) * Mathf.Exp(-Pull);

  private readonly Timeline _frames = Timeline.Periodic(1 / 60f);
  private readonly State<int> _played = new(0);
  private float _startedAt = float.NegativeInfinity;
  private float _seconds = .3f;

  /// <summary>How many times it was asked to play, moving or not.</summary>
  public int Asked { get; private set; }

  /// <summary>Arrives once from now, over <paramref name="seconds"/>; it is at rest at once while <paramref name="reduceMotion"/>.</summary>
  public void Play(float seconds, bool reduceMotion = false)
  {
    Asked++;
    if (reduceMotion || seconds <= 0)
    {
      _startedAt = float.NegativeInfinity;
      _played.Value++;
      return;
    }
    _seconds = seconds;
    _startedAt = Time.unscaledTime;
    _played.Value++;
    _frames.Restart();
  }

  /// <summary>Still on its way. Nothing follows this by itself: ask it each frame.</summary>
  public bool Running => Time.unscaledTime - _startedAt < _seconds;

  /// <summary>How far along it is: 0 as it starts, 1 at rest. Reading it follows the arrival while it runs.</summary>
  public float Progress
  {
    [Pure]
    get
    {
      // Each play starts the readers again.
      _ = _played.Value;
      float time = Time.unscaledTime - _startedAt;
      if (time < 0 || time >= _seconds)
        return 1;
      // Only while it runs is each frame followed.
      _ = _frames.Tick;
      float pulled = Pull * time / _seconds;
      return (1 - (1 + pulled) * Mathf.Exp(-pulled)) / Reached;
    }
  }

  public void Dispose() => _frames.Dispose();
}
