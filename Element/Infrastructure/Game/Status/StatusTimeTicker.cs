using System;
using System.Diagnostics;
using Element.Application;
using Element.Domain.Status;
using UnityEngine;

namespace Element.Infrastructure.Game.Status;

internal sealed class StatusTimeTicker : MonoBehaviour
{
  private const double RefreshIntervalSeconds = 0.1d;

  private readonly FrameRateSampler _frameRateSampler = new();
  private Action<int> _tick;
  private IRenderVideo _video;
  private double _nextRefreshTime;
  private bool _running;
  private bool _followingVideo;

  public void Initialize(Action<int> tick, IRenderVideo video)
  {
    _tick = tick;
    _video = video;
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

    // OrbitRender draws the video slower than real time and repeats frames for
    // overlays, so the screen's frame rate says nothing about the video. FPS
    // shows "-" and the time follows every frame.
    if (_video?.IsRendering == true)
    {
      _followingVideo = true;
      _tick?.Invoke(StatusSnapshot.UnknownFps);
      return;
    }
    if (_followingVideo)
    {
      _followingVideo = false;
      _frameRateSampler.Reset();
    }

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
