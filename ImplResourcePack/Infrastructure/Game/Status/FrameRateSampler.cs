using System;
using System.Collections.Generic;

namespace ImplResourcePack.Infrastructure.Game.Status;

internal sealed class FrameRateSampler
{
  private const double WindowSeconds = 1d;
  private const double MinimumSampleSeconds = 0.25d;

  private readonly Queue<double> _frameTimes = new();
  private double _lastFrameTime = double.NaN;

  public void Reset()
  {
    _frameTimes.Clear();
    _lastFrameTime = double.NaN;
  }

  public bool TryRecordFrame(double nowSeconds, out int framesPerSecond)
  {
    framesPerSecond = 0;
    if (double.IsNaN(nowSeconds) || double.IsInfinity(nowSeconds))
      return false;

    if (!double.IsNaN(_lastFrameTime) && nowSeconds <= _lastFrameTime)
      Reset();

    _lastFrameTime = nowSeconds;
    _frameTimes.Enqueue(nowSeconds);

    double cutoff = nowSeconds - WindowSeconds;
    while (_frameTimes.Count > 2 && _frameTimes.Peek() < cutoff)
      _frameTimes.Dequeue();

    if (_frameTimes.Count < 2)
      return false;

    double elapsed = nowSeconds - _frameTimes.Peek();
    if (elapsed < MinimumSampleSeconds)
      return false;

    double measured = (_frameTimes.Count - 1) / elapsed;
    if (measured <= 0d || double.IsNaN(measured) || double.IsInfinity(measured))
      return false;

    framesPerSecond = Math.Max(1, (int)Math.Round(measured));
    return true;
  }
}
