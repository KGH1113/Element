using System;
using System.Globalization;
using System.Text;
using System.Threading;
using ImplResourcePack.Application.KeyLimiter;
using ImplResourcePack.Domain.Input;
using ImplResourcePack.Infrastructure.Game.Input;
using ImplResourcePack.Presentation.Overlay.Shared;
using UnityEngine;

namespace ImplResourcePack.Tests;

internal static class PerformanceTests
{
  public static void Run()
  {
    FormatsNumbersLikeTheOriginalStrings();
    AppendsWithoutAllocating();
    HookThreadReadsOnlyPublishedState();
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

  private static void HookThreadReadsOnlyPublishedState()
  {
    KeyLimiterService service = new(new DmNoteKeyResolver());
    service.Apply(
      new KeyLimiterUpdate
      {
        SchemaVersion = 2,
        Source = "impl-dm-note",
        ClientVersion = "0.1.0",
        SessionId = "hook-thread",
        Revision = 1,
        Enabled = true,
        Profile = new KeyLimiterProfile { Id = "4key", Name = "4key" },
        Keys = new[] { "A" },
        ExcludedGhostKeys = Array.Empty<string>(),
      }
    );
    KeyLimiterSnapshot snapshot = service.Current;
    bool[] results = new bool[4];
    Exception failure = null;
    // The native hook is not a Unity thread. Any Unity object access in the
    // policy would throw here, because this process has no Unity runtime.
    var thread = new Thread(() =>
    {
      try
      {
        KeyLimiterPatchPolicy.Publish(true);
        results[0] = KeyLimiterPatchPolicy.ShouldEnforceOnHookThread(snapshot);
        results[1] = KeyLimiterPatchPolicy.ShouldEnforceOnHookThread(null);
        KeyLimiterPatchPolicy.Reset();
        results[2] = KeyLimiterPatchPolicy.ShouldEnforceOnHookThread(snapshot);
        service.Clear();
        KeyLimiterPatchPolicy.Publish(true);
        results[3] = KeyLimiterPatchPolicy.ShouldEnforceOnHookThread(service.Current);
      }
      catch (Exception exception)
      {
        failure = exception;
      }
      finally
      {
        KeyLimiterPatchPolicy.Reset();
      }
    });
    thread.Start();
    thread.Join();
    if (failure != null)
      throw new InvalidOperationException("The hook-thread policy touched Unity state.", failure);
    True(results[0], "published player control must enforce an enabled profile");
    True(!results[1], "a missing profile must never block keys");
    True(!results[2], "after a scene unload the hook must stop enforcing");
    True(!results[3], "a disabled profile must never block keys");
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
