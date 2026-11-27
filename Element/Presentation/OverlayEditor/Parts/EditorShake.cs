using System;
using ElementUI;
using UnityEngine;

namespace Element.Presentation.OverlayEditor.Parts;

/// <summary>
/// A short sideways shake that says "no" where words would: what can't be
/// finished, or can't take what was given, shakes for a moment and is still
/// again. A view offsets itself by <see cref="X"/>; nothing runs while it
/// rests. With reduced motion it stays still, and its owner's color says it.
/// </summary>
internal sealed class EditorShake : IDisposable
{
  private const float Duration = .36f,
    Reach = 7,
    Swings = 3.5f;

  private readonly Timeline _frames = Timeline.Periodic(1 / 60f);
  private readonly State<int> _played = new(0);
  private float _startedAt = float.NegativeInfinity;

  /// <summary>How many times it was asked to shake, moving or not.</summary>
  public int Asked { get; private set; }

  /// <summary>Shakes once from now; it stays still while <paramref name="reduceMotion"/>.</summary>
  public void Play(bool reduceMotion = false)
  {
    Asked++;
    if (reduceMotion)
      return;
    _startedAt = Time.unscaledTime;
    _played.Value++;
    _frames.Restart();
  }

  /// <summary>How far aside it is now, in units; 0 at rest. Reading it follows the shake while it runs.</summary>
  public float X
  {
    [Pure]
    get
    {
      // Each play starts the readers again.
      _ = _played.Value;
      float time = Time.unscaledTime - _startedAt;
      if (time < 0 || time >= Duration)
        return 0;
      // Only while it runs is each frame followed.
      _ = _frames.Tick;
      float left = 1 - time / Duration;
      return Reach * left * Mathf.Sin(time / Duration * Swings * 2 * Mathf.PI);
    }
  }

  public void Dispose() => _frames.Dispose();
}
