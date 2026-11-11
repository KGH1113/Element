using System;
using Element.Application.Attempts;
using Element.Application.Bpm;
using Element.Application.Judgement;
using Element.Application.Status;
using Element.Domain.Judgement;

namespace Element.Application.Overlay;

/// <summary>
/// The overlay's values as one: all of them show, hide and refresh together, and each
/// game event refreshes the ones it changes.
/// </summary>
internal sealed class OverlayCoordinator : IDisposable
{
  private readonly JudgementFeature _judgement;
  private readonly StatusFeature _status;
  private readonly BpmFeature _bpm;
  private readonly ComboFeature _combo;
  private readonly TimingScaleFeature _timingScale;
  private readonly IOverlayFeature[] _all;
  private bool _visible;
  private bool _disposed;

  public bool IsVisible => _visible && !_disposed;

  public OverlayCoordinator(
    JudgementFeature judgement,
    StatusFeature status,
    BpmFeature bpm,
    ComboFeature combo,
    TimingScaleFeature timingScale,
    AttemptFeature attempt
  )
  {
    _judgement = judgement;
    _status = status;
    _bpm = bpm;
    _combo = combo;
    _timingScale = timingScale;
    _all = new IOverlayFeature[] { judgement, status, bpm, combo, timingScale, attempt };
  }

  public void Show()
  {
    if (_disposed)
      return;
    if (_visible)
    {
      RefreshAll();
      return;
    }

    _visible = true;
    for (int i = 0; i < _all.Length; i++)
      _all[i].Show();
  }

  public void Hide()
  {
    if (!_visible)
      return;

    _visible = false;
    for (int i = 0; i < _all.Length; i++)
      _all[i].Hide();
  }

  public void RefreshAll()
  {
    if (!IsVisible)
      return;

    for (int i = 0; i < _all.Length; i++)
      _all[i].Refresh();
  }

  /// <param name="counts">The judgement counts after the hit; null when they are not the single player's.</param>
  public void RegisterHit(JudgementSnapshot? counts, HitMargin hit)
  {
    if (!IsVisible)
      return;

    if (counts.HasValue)
      _judgement.Refresh(counts.Value);
    _combo.RegisterHit(hit);
  }

  public void RefreshFloor()
  {
    if (!IsVisible)
      return;

    _status.RefreshMetrics();
    _bpm.Refresh();
    _timingScale.Refresh();
  }

  public void RefreshAccuracy()
  {
    if (IsVisible)
      _status.RefreshMetrics();
  }

  public void RefreshTime(int framesPerSecond)
  {
    if (IsVisible)
      _status.RefreshTime(framesPerSecond);
  }

  /// <param name="counts">The judgement counts after the reset; null when they are not the single player's.</param>
  public void RefreshAfterReset(JudgementSnapshot? counts)
  {
    if (!IsVisible)
      return;

    if (counts.HasValue)
      _judgement.Refresh(counts.Value);
    _status.Refresh();
    _bpm.Refresh();
    _combo.Reset();
    _timingScale.Refresh();
  }

  public void Dispose()
  {
    if (_disposed)
      return;

    Hide();
    _disposed = true;
    for (int i = 0; i < _all.Length; i++)
      _all[i].Dispose();
  }
}
