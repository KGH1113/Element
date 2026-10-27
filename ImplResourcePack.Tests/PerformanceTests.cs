using System;
using System.Globalization;
using System.Text;
using System.Threading;
using ImplResourcePack.Domain.Input;
using ImplResourcePack.Presentation.Overlay.Shared;
using UnityEngine;

namespace ImplResourcePack.Tests;

internal static class PerformanceTests
{
  public static void Run()
  {
    FormatsNumbersLikeTheOriginalStrings();
    AppendsWithoutAllocating();
    ChatterFilterDropsBounceAndItsRelease();
    ChatterFilterKeepsFastJacksAndRepeats();
    ChatterFilterRunsOffTheMainThreadWithoutAllocating();
  }

  private static void FormatsNumbersLikeTheOriginalStrings()
  {
    foreach (long value in new[] { 0L, 7, 10, 120, 98765, -1, -42, long.MaxValue, long.MinValue })
      Equal(value.ToString(CultureInfo.InvariantCulture), new StringBuilder().AppendInt(value).ToString());

    foreach (double value in new[] { 0d, 1, 99.5, 99.57, 99.999, 100, 0.05, 0.004, 12.345, 180, 7999.995, -3.25 })
      for (int decimals = 0; decimals <= 2; decimals++)
        Equal(
          Math.Round(value, decimals).ToString(CultureInfo.InvariantCulture),
          new StringBuilder().AppendRounded(value, decimals).ToString()
        );

    Equal("05", new StringBuilder().AppendTwoDigits(5).ToString());
    Equal("59", new StringBuilder().AppendTwoDigits(59).ToString());
    Equal("FF8000", new StringBuilder().AppendHexRgb(new Color(1f, .5f, 0f)).ToString());
    Equal("D958FF", new StringBuilder().AppendHexRgb(new Color(0.8509804f, 0.345098f, 1f)).ToString());
    True(NumberText.Scaled(99.571, 2) == NumberText.Scaled(99.569, 2), "equal displayed values must compare equal");
    True(NumberText.Scaled(double.NaN, 2) == 0, "non-finite values must not overflow");
  }

  private static void AppendsWithoutAllocating()
  {
    var builder = new StringBuilder(256);
    Fill(builder);
    long before = GC.GetAllocatedBytesForCurrentThread();
    for (int index = 0; index < 1000; index++)
      Fill(builder);
    long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
    True(allocated == 0, "overlay number formatting allocated " + allocated + " bytes");
  }

  private static void Fill(StringBuilder builder)
  {
    builder.Clear().AppendInt(123456).Append(' ').AppendRounded(99.57, 2).Append(' ');
    builder.AppendTwoDigits(7).Append(' ').AppendHexRgb(new Color(.2f, .4f, .6f));
  }

  private const long Millisecond = TimeSpan.TicksPerMillisecond;

  private static void ChatterFilterDropsBounceAndItsRelease()
  {
    var filter = new ChatterFilter();
    filter.Configure(true, 20);
    long t = 1_000_000 * Millisecond;
    True(filter.Accept(30, true, t), "the first press passes");
    True(filter.Accept(30, false, t + 80 * Millisecond), "its release passes");
    True(!filter.Accept(30, true, t + 83 * Millisecond), "a press 3 ms after release is bounce");
    True(!filter.Accept(30, false, t + 86 * Millisecond), "the bounce's release is swallowed too");
    True(!filter.Accept(30, true, t + 95 * Millisecond), "bounce right after a swallowed release is caught");
    True(!filter.Accept(30, false, t + 97 * Millisecond), "and its release");
    True(filter.Accept(30, true, t + 140 * Millisecond), "a real press after the window passes");
    True(filter.Accept(31, true, t + 141 * Millisecond), "other keys are independent");
    True(filter.Blocked == 2, "two bounces were counted, got " + filter.Blocked);
  }

  private static void ChatterFilterKeepsFastJacksAndRepeats()
  {
    var filter = new ChatterFilter();
    filter.Configure(true, 20);
    long t = 5_000_000 * Millisecond;
    // 16th notes at 300 BPM: 50 ms apart, held 25 ms.
    for (int i = 0; i < 8; i++)
    {
      True(filter.Accept(12, true, t + i * 50 * Millisecond), "jack press " + i + " passes");
      True(filter.Accept(12, false, t + (i * 50 + 25) * Millisecond), "jack release " + i + " passes");
    }
    True(filter.Accept(13, true, t), "a held key passes");
    True(filter.Accept(13, true, t + 500 * Millisecond), "OS key repeat of a held key passes");
    True(filter.Accept(-1, true, t) && filter.Accept(5000, true, t), "out-of-range keys pass");
    filter.Configure(false, 20);
    True(filter.Accept(12, true, t + 401 * Millisecond), "a disabled filter passes everything");
    True(filter.Blocked == 0, "nothing was blocked");
  }

  // SkyHook calls the filter on a native thread with no Unity runtime and
  // expects no allocation per event.
  private static void ChatterFilterRunsOffTheMainThreadWithoutAllocating()
  {
    var filter = new ChatterFilter();
    filter.Configure(true, 20);
    long allocated = -1;
    Exception failure = null;
    var thread = new Thread(() =>
    {
      try
      {
        long t = 1_000_000 * Millisecond;
        filter.Accept(1, true, t);
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 10_000; i++)
        {
          t += 7 * Millisecond;
          filter.Accept(i & 255, (i & 1) == 0, t);
        }
        allocated = GC.GetAllocatedBytesForCurrentThread() - before;
      }
      catch (Exception exception)
      {
        failure = exception;
      }
    });
    thread.Start();
    thread.Join();
    if (failure != null)
      throw new InvalidOperationException("The chatter filter failed off the main thread.", failure);
    True(allocated == 0, "the chatter filter allocated " + allocated + " bytes");
  }

  private static void True(bool value, string message)
  {
    if (!value)
      throw new InvalidOperationException(message);
  }

  private static void Equal(string expected, string actual)
  {
    if (expected != actual)
      throw new InvalidOperationException("Expected " + expected + ", got " + actual);
  }
}
