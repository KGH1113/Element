using System;
using Element.Infrastructure.Game.Play;
using Element.Tests.Support;
using static Element.Tests.Support.Assert;

namespace Element.Tests.Infrastructure.Game;

/// <summary>The frame rate the overlay shows.</summary>
internal static class FrameRateTests
{
  [Suite("measures stable and briefly dropped frame rates")]
  public static void MeasuresFrameRates()
  {
    Equal(60, MeasureFrameRate(60d, 1d));
    Equal(50, MeasureFrameRate(50d, 1d));

    FrameRateSampler sampler = new();
    double now = 0d;
    int measured = 0;
    for (int frame = 0; frame < 60; frame++)
    {
      now += frame == 30 ? 2d / 60d : 1d / 60d;
      sampler.TryRecordFrame(now, out measured);
    }

    Check(measured >= 58 && measured <= 60, "one missed refresh must not be exaggerated as 50 FPS");
  }

  private static int MeasureFrameRate(double rate, double duration)
  {
    FrameRateSampler sampler = new();
    int measured = 0;
    int frameCount = (int)Math.Ceiling(rate * duration) + 1;
    for (int frame = 0; frame < frameCount; frame++)
      sampler.TryRecordFrame(frame / rate, out measured);
    return measured;
  }
}
