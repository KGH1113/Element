using UnityEngine;

namespace Element.Presentation.Layout;

/// <summary>
/// A number that glides to where it is told over a short ease-out, starting
/// from wherever it is now, so a change partway turns smoothly instead of
/// jumping back. The editors glide the space their preview centers in, so
/// opening the color picker slides the preview aside (shrinking it if it no
/// longer fits) and closing it slides it back. With reduced motion it jumps.
/// </summary>
internal sealed class OverlayGlide
{
  private const float Duration = .34f;

  private float _from,
    _to,
    _startedAt;
  private bool _placed;

  /// <summary>Still on its way; the editor redraws every frame until it arrives.</summary>
  public bool Moving { get; private set; }

  /// <summary>Where it is this frame on its way to <paramref name="target"/>.</summary>
  public float Toward(float target, bool reduceMotion)
  {
    float now = Time.unscaledTime;
    if (!_placed || reduceMotion)
    {
      _from = _to = target;
      _placed = true;
      Moving = false;
      return target;
    }
    if (!Mathf.Approximately(target, _to))
    {
      _from = At(now);
      _to = target;
      _startedAt = now;
    }
    float value = At(now);
    Moving = now - _startedAt < Duration;
    return value;
  }

  /// <summary>Takes the next target at once, as when the editor is laid out afresh.</summary>
  public void Snap() => _placed = false;

  // Ease-out cubic: quick to leave, settling softly, never overshooting.
  private float At(float now)
  {
    float t = Mathf.Clamp01((now - _startedAt) / Duration);
    float eased = 1 - (1 - t) * (1 - t) * (1 - t);
    return Mathf.LerpUnclamped(_from, _to, eased);
  }
}
