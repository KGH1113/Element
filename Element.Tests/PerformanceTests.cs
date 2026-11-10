using System;
using System.Globalization;
using System.Text;
using System.Threading;
using Element.Domain.Input;
using Element.Presentation.Overlay.Shared;
using UnityEngine;

namespace Element.Tests;

internal static class PerformanceTests
{
  public static void Run()
  {
    FormatsNumbersLikeTheOriginalStrings();
    AppendsWithoutAllocating();
    ChatterFilterDropsPressesWithinTheThreshold();
    ChatterFilterFollowsSettings();
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

  private static readonly bool[] NoIgnoredKeys = new bool[ChatterFilter.KeyCount];

  // KeyboardChatterBlocker's rule: a press within the threshold of the same
  // key's last accepted press is dropped; releases never reach the filter.
  private static void ChatterFilterDropsPressesWithinTheThreshold()
  {
    var filter = new ChatterFilter(sameFrame: false);
    filter.Configure(true, 100, NoIgnoredKeys);
    long t = 1_700_000_000_000;
    True(filter.AcceptPress(30, t), "the first press passes");
    True(!filter.AcceptPress(30, t + 40), "a press 40 ms later is chatter");
    True(!filter.AcceptPress(30, t + 100), "a press exactly at the threshold is chatter");
    True(filter.AcceptPress(30, t + 101), "a press past the threshold passes");
    True(!filter.AcceptPress(30, t + 150), "a dropped press doesn't move the time");
    True(filter.AcceptPress(31, t + 150), "other keys are independent");
    True(!filter.AcceptPress(30, t + 90), "async input drops a press from before the last one");
    True(filter.Blocked == 4, "four presses were dropped, got " + filter.Blocked);

    // 16th notes at 300 BPM are 50 ms apart: every other one is dropped, as
    // with KeyboardChatterBlocker at 100 ms.
    var jacks = new ChatterFilter(sameFrame: false);
    jacks.Configure(true, 100, NoIgnoredKeys);
    int passed = 0;
    for (int i = 0; i < 8; i++)
      if (jacks.AcceptPress(12, t + i * 50))
        passed++;
    True(passed == 3, "50 ms jacks on one key keep every third press at 100 ms, got " + passed);
  }

  private static void ChatterFilterFollowsSettings()
  {
    long t = 1_700_000_000_000;
    var unity = new ChatterFilter(sameFrame: true);
    unity.Configure(true, 100, NoIgnoredKeys);
    True(unity.AcceptPress(5, t), "a Unity press passes");
    True(unity.AcceptPress(5, t + 3), "Unity input asking again within 5 ms gets the same answer");
    True(!unity.AcceptPress(5, t + 9), "past 5 ms it is chatter again");
    True(unity.AcceptPress(5, t - 50), "Unity input accepts a press from before the last one");

    var ignored = new bool[ChatterFilter.KeyCount];
    ignored[44] = true;
    var filter = new ChatterFilter(sameFrame: false);
    filter.Configure(true, 100, ignored);
    True(filter.AcceptPress(44, t) && filter.AcceptPress(44, t + 1), "an ignored key always passes");
    filter.Configure(true, 100, NoIgnoredKeys);
    True(filter.AcceptPress(44, t + 2), "an ignored key left no press time behind");
    True(!filter.AcceptPress(44, t + 3), "and is filtered once off the list");
    filter.Configure(true, 200, NoIgnoredKeys);
    True(!filter.AcceptPress(44, t + 150), "a new threshold keeps the press times");
    True(filter.AcceptPress(-1, t) && filter.AcceptPress(5000, t), "out-of-range keys pass");
    filter.Configure(false, 100, NoIgnoredKeys);
    True(!filter.Enabled && filter.AcceptPress(44, t + 151), "a disabled filter passes everything");
    True(
      ChatterFilter.ClampWindow(1) == ChatterFilter.MinimumWindowMs
        && ChatterFilter.ClampWindow(5000) == ChatterFilter.MaximumWindowMs
        && ChatterFilter.DefaultWindowMs == 100,
      "the threshold is clamped and starts at KeyboardChatterBlocker's 100 ms"
    );
  }

  // SkyHook calls the filter on a native thread with no Unity runtime and
  // expects no allocation per event.
  private static void ChatterFilterRunsOffTheMainThreadWithoutAllocating()
  {
    var filter = new ChatterFilter(sameFrame: false);
    filter.Configure(true, 100, NoIgnoredKeys);
    long allocated = -1;
    Exception failure = null;
    var thread = new Thread(() =>
    {
      try
      {
        long t = 1_700_000_000_000;
        filter.AcceptPress(1, t);
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 10_000; i++)
        {
          t += 7;
          filter.AcceptPress(i & 255, t);
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
