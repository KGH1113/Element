using System;
using ImplResourcePack.Application.Features;

namespace ImplResourcePack.Application;

internal sealed class OverlayCoordinator : IDisposable
{
  private readonly JudgementFeature _judgement;
  private readonly StatusFeature _status;
  private readonly BpmFeature _bpm;
  private readonly ComboFeature _combo;
  private bool _visible;
  private bool _disposed;

  public bool IsVisible => _visible && !_disposed;

  public OverlayCoordinator(JudgementFeature judgement, StatusFeature status, BpmFeature bpm, ComboFeature combo)
  {
    _judgement = judgement;
    _status = status;
    _bpm = bpm;
    _combo = combo;
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
    _judgement.Show();
    _status.Show();
    _bpm.Show();
    _combo.Show();
  }

  public void Hide()
  {
    if (!_visible)
      return;

    _visible = false;
    _judgement.Hide();
    _status.Hide();
    _bpm.Hide();
    _combo.Hide();
  }

  public void RefreshAll()
  {
    if (!IsVisible)
      return;

    _judgement.Refresh();
    _status.RefreshAll();
    _bpm.Refresh();
    _combo.Refresh();
  }

  public void RegisterHit(scrMarginTracker tracker, HitMargin hit)
  {
    if (!IsVisible)
      return;

    _judgement.Refresh(tracker);
    _combo.RegisterHit(hit);
  }

  public void RefreshFloor()
  {
    if (!IsVisible)
      return;

    _status.RefreshMetrics();
    _bpm.Refresh();
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

  public void RefreshAfterReset(scrMarginTracker tracker)
  {
    if (!IsVisible)
      return;

    _judgement.Refresh(tracker);
    _status.RefreshAll();
    _bpm.Refresh();
    _combo.Reset();
  }

  public void Dispose()
  {
    if (_disposed)
      return;

    Hide();
    _disposed = true;
    _judgement.Dispose();
    _status.Dispose();
    _bpm.Dispose();
    _combo.Dispose();
  }
}
