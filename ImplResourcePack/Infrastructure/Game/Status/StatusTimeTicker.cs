using System;
using System.Diagnostics;
using UnityEngine;

namespace ImplResourcePack.Infrastructure.Game.Status;

internal sealed class StatusTimeTicker : MonoBehaviour
{
  private const double RefreshIntervalSeconds = 0.1d;

  private readonly FrameRateSampler _frameRateSampler = new();
  private Action<int> _tick;
  private double _nextRefreshTime;
  private bool _running;

  public void Initialize(Action<int> tick)
  {
    _tick = tick;
  }

  public void StartTicking()
  {
    if (_running || _tick == null)
      return;

    _frameRateSampler.Reset();
    _nextRefreshTime = GetRealtimeSeconds();
    _running = true;
  }

  public void StopTicking()
  {
    if (!_running)
      return;

    _running = false;
    _frameRateSampler.Reset();
  }

  private void Update()
  {
    if (!_running)
      return;

    double now = GetRealtimeSeconds();
    if (!_frameRateSampler.TryRecordFrame(now, out int framesPerSecond) || now < _nextRefreshTime)
      return;

    _nextRefreshTime = now + RefreshIntervalSeconds;
    _tick?.Invoke(framesPerSecond);
  }

  private static double GetRealtimeSeconds() => (double)Stopwatch.GetTimestamp() / Stopwatch.Frequency;

  private void OnDisable()
  {
    StopTicking();
  }

  private void OnDestroy()
  {
    StopTicking();
    _tick = null;
  }
}
