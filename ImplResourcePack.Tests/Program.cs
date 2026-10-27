using System;
using System.Threading;
using ImplResourcePack.Application.KeyLimiter;
using ImplResourcePack.Domain.Judgement;
using ImplResourcePack.Infrastructure.Game.Input;
using ImplResourcePack.Infrastructure.Game.Judgement;
using ImplResourcePack.Infrastructure.Game.Status;
using ImplResourcePack.Presentation.Overlay.Combo;
using UnityEngine;

namespace ImplResourcePack.Tests;

internal static class Program
{
  private static int _passed;

  public static int Main()
  {
    try
    {
      Run("normalizes DM Note names", NormalizeNames);
      Run("maps representative keyboard, mouse, and gamepad keys", MapsRepresentativeKeys);
      Run("maps macOS modifier async keys", MapsMacModifierAsyncKeys);
      Run("maps Enter async keys", MapsEnterAsyncKeys);
      Run("allows ANSI and ISO backslash input", AllowsBackslashInput);
      Run("applies supported keys and reports unsupported keys", AppliesSupportedSubset);
      Run("handles duplicate and stale revisions", HandlesRevisionOrdering);
      Run("profile ownership and disconnect restore", HandlesProfileOwnership);
      Run("canceled queued profile cannot regain ownership", RejectsCanceledProfile);
      Run("disables when no requested key is supported", DisablesUnsupportedOnlyProfile);
      Run("applies hit text visibility settings", AppliesHitTextVisibilitySettings);
      Run("classifies X-Perfect hit margins", ClassifiesXPerfectHitMargins);
      Run("represents X-Perfect counts separately", RepresentsXPerfectCountsSeparately);
      Run("normalizes song title size tags", NormalizesSongTitleSizeTags);
      Run("measures stable and briefly dropped frame rates", MeasuresFrameRates);
      Run("trackpad gesture ownership, zoom and native ABI", TrackpadTests.Run);
      Run("overlay layout hit bounds and eight anchored proportional resize handles", OverlayLayoutTests.Run);
      Run("layout history, native viewer recovery and alignment snapping", LayoutEditingTests.Run);
      Run("native viewers accept only bounded loopback OBS surfaces", NativeViewerTests.Run);
      Run("gameplay tweaks, judgement filtering, editor transforms and planet persistence", GameplayTweaksTests.Run);
      Run("effect presets, source preservation and stable editor filtering", EffectSettingsTests.Run);
      Run("allocation-free overlay text and hook-thread chatter filtering", PerformanceTests.Run);
      Run("effect filter copies only changed tracks and ignores NONE tags", EffectFilterTests.Run);
      Run("native viewer sessions follow pushes and keep pages on transient failures", NativeViewerSessionTests.Run);
      Console.WriteLine("ImplResourcePack tests: " + _passed + " passed");
      return 0;
    }
    catch (Exception exception)
    {
      Console.Error.WriteLine(exception);
      return 1;
    }
  }

  private static void NormalizeNames()
  {
    Equal("LEFTSHIFT", DmNoteKeyMapper.Normalize(" left shift "));
    Equal("GPA", DmNoteKeyMapper.Normalize("GP_A"));
  }

  private static void MapsRepresentativeKeys()
  {
    MapsTo("A", KeyCode.A);
    MapsTo("LEFT SHIFT", KeyCode.LeftShift);
    MapsTo("25", KeyCode.RightControl);
    MapsTo("FORWARD SLASH", KeyCode.Slash);
    MapsTo("NUMPAD RETURN", KeyCode.KeypadEnter);
    MapsTo("MOUSE5", KeyCode.Mouse4);
    MapsTo("GP_A", KeyCode.JoystickButton0);
    MapsTo("GP_RIGHT", KeyCode.JoystickButton15);
  }

  private static void MapsMacModifierAsyncKeys()
  {
    MacModifierMapsTo(KeyCode.LeftControl, 0xE0);
    MacModifierMapsTo(KeyCode.LeftShift, 0xE1);
    MacModifierMapsTo(KeyCode.LeftAlt, 0xE2);
    MacModifierMapsTo(KeyCode.LeftMeta, 0xE3);
    MacModifierMapsTo(KeyCode.RightControl, 0xE4);
    MacModifierMapsTo(KeyCode.RightShift, 0xE5);
    MacModifierMapsTo(KeyCode.RightAlt, 0xE6);
    MacModifierMapsTo(KeyCode.RightMeta, 0xE7);
    True(!DmNoteKeyMapper.TryGetMacAsyncKey(KeyCode.A, out _), "ordinary keys must not use the modifier fallback");
    MapsAsyncTo("LEFT SHIFT", KeyCode.LeftShift, 0xE1);
    MapsAsyncTo("RIGHT SHIFT", KeyCode.RightShift, 0xE5);
  }

  private static void MapsEnterAsyncKeys()
  {
    MapsAsyncTo("RETURN", KeyCode.Return, 0x28);
    MapsAsyncTo("NUMPAD RETURN", KeyCode.KeypadEnter, 0x58);
  }

  private static void AppliesSupportedSubset()
  {
    KeyLimiterService service = new(new DmNoteKeyResolver());
    KeyLimiterApplyResult response = service.Apply(Request(1, true, "A", "FN", "DOT"));
    True(response.Ok && response.Applied && response.Enabled, "request should be applied");
    Sequence(new[] { "A", "DOT" }, response.SupportedKeys);
    Sequence(new[] { "FN" }, response.UnsupportedKeys);
    True(service.Current.Allows(KeyCode.A), "A should be allowed");
    True(!service.Current.Allows(KeyCode.B), "B should be blocked");
    // ADOFAI keeps Escape out of main input itself, so the profile never needs it.
    True(!service.Current.Allows(KeyCode.Escape), "Escape is not part of the profile");
  }

  private static void AllowsBackslashInput()
  {
    foreach (string name in new[] { "BACKSLASH", "\\" })
    {
      KeyLimiterService service = new(new DmNoteKeyResolver());
      service.Apply(Request(1, true, name));
      True(service.Current.Allows(KeyCode.Backslash), "backslash should be allowed");
      if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.OSX))
      {
        True(service.Current.AllowsAsync(0x31), "ANSI backslash should be allowed");
        True(service.Current.AllowsAsync(0x64), "ISO backslash should be allowed");
        True(!service.Current.AllowsAsync(0x38), "forward slash must remain blocked");
        service.Apply(Request(2, true, "A"));
        True(!service.Current.AllowsAsync(0x31), "removed ANSI backslash must be blocked");
        True(!service.Current.AllowsAsync(0x64), "removed ISO backslash must be blocked");
      }
    }
  }

  private static void HandlesRevisionOrdering()
  {
    KeyLimiterService service = new(new DmNoteKeyResolver());
    service.Apply(Request(2, true, "A"));
    Equal("duplicate", service.Apply(Request(2, true, "S")).Reason);
    Equal("stale_revision", service.Apply(Request(1, true, "S")).Reason);
    True(service.Current.Allows(KeyCode.A), "stale requests must not replace state");
  }

  private static void DisablesUnsupportedOnlyProfile()
  {
    KeyLimiterService service = new(new DmNoteKeyResolver());
    KeyLimiterApplyResult response = service.Apply(Request(1, true, "FN"));
    True(response.Applied && !response.Enabled, "unsupported-only profile must fail open");
  }

  private static void AppliesHitTextVisibilitySettings()
  {
    True(HitTextVisibilityPolicy.ShouldShow(false, false, true), "Perfect should show when its setting is disabled");
    True(!HitTextVisibilityPolicy.ShouldShow(true, false, true), "Perfect should hide when its setting is enabled");
    True(
      HitTextVisibilityPolicy.ShouldShow(true, false, false),
      "non-Perfect judgments should remain visible outside record mode"
    );
    True(!HitTextVisibilityPolicy.ShouldShow(false, true, false), "record mode should hide every judgment");
  }

  private static void ClassifiesXPerfectHitMargins()
  {
    foreach (HitMargin hit in new[] { HitMargin.PerfectMinus, HitMargin.XPerfect, HitMargin.PerfectPlus })
    {
      True(HitMarginPolicy.IsPerfect(hit), hit + " should be a perfect judgment");
      True(HitMarginPolicy.ContinuesCombo(hit), hit + " should continue the combo");
    }

    True(!HitMarginPolicy.IsPerfect(HitMargin.Auto), "Auto should not be shown as a perfect judgment");
    True(HitMarginPolicy.ContinuesCombo(HitMargin.Auto), "Auto should continue the combo");
    True(!HitMarginPolicy.IsPerfect(HitMargin.Multipress), "Multipress should remain distinct");
    True(!HitMarginPolicy.ContinuesCombo(HitMargin.EarlyPerfect), "EarlyPerfect should reset the combo");
  }

  private static void RepresentsXPerfectCountsSeparately()
  {
    JudgementSnapshot snapshot = new(1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11);

    Equal(5, snapshot.PerfectMinus);
    Equal(6, snapshot.XPerfectAndAuto);
    Equal(7, snapshot.PerfectPlus);
  }

  private static void NormalizesSongTitleSizeTags()
  {
    Equal(
      "<size=150%>large</size> <size=50%>small</size>",
      SongTitleMarkupNormalizer.Normalize("<size=150>large</size> <size=50>small</size>", 100)
    );
    Equal(
      "<size=125%>larger</size> <size=75%>smaller</size>",
      SongTitleMarkupNormalizer.Normalize("<size=+25>larger</size> <size=-25>smaller</size>", 100)
    );
    Equal(
      "<size=60%>percent</size> <size=1.2em>em</size>",
      SongTitleMarkupNormalizer.Normalize("<size=60%>percent</size> <size=1.2em>em</size>", 100)
    );
    Equal("<size=145.833333%>scaled</size>", SongTitleMarkupNormalizer.Normalize("<size=70>scaled</size>", 48));
    Equal("plain", SongTitleMarkupNormalizer.Normalize("plain", 0));
  }

  private static void MeasuresFrameRates()
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

    True(measured >= 58 && measured <= 60, "one missed refresh must not be exaggerated as 50 FPS");
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

  private static void HandlesProfileOwnership()
  {
    KeyLimiterService profiles = new(new DmNoteKeyResolver());
    KeyLimiterSessionController session = new(profiles);
    True(session.Apply("first-app", Request(1, true, "A")).Applied, "First app must own the profile.");
    True(
      session.Apply("second-app", Request(2, true, "B")).Reason == "profile_owned_by_another_app",
      "A second app cannot replace the profile."
    );
    True(
      !session.PeerDisconnected("second-app") && profiles.Current.Enabled,
      "An unrelated disconnect must preserve the owner."
    );
    True(
      session.PeerDisconnected("first-app") && !profiles.Current.Enabled,
      "Owner disconnect must release the key limiter."
    );
    True(
      session.Apply("second-app", Request(1, true, "B")).Applied,
      "A new connection can become owner after release."
    );
    KeyLimiterUpdate invalid = Request(2, true, "A");
    invalid.SchemaVersion = 1;
    True(
      session.Apply("second-app", invalid).Reason == "unsupported_schema",
      "The old profile schema must be rejected."
    );
    session.Clear();
    True(!profiles.Current.Enabled, "Mod shutdown must release the profile.");
  }

  private static void RejectsCanceledProfile()
  {
    KeyLimiterService profiles = new(new DmNoteKeyResolver());
    KeyLimiterSessionController session = new(profiles);
    using CancellationTokenSource disconnected = new();
    session.Apply("old-app", Request(1, true, "A"));
    disconnected.Cancel();
    session.PeerDisconnected("old-app");
    bool rejected = false;
    try
    {
      session.Apply("old-app", Request(2, true, "B"), disconnected.Token);
    }
    catch (OperationCanceledException)
    {
      rejected = true;
    }
    True(rejected && !profiles.Current.Enabled, "Disconnected queued work must not re-enable input blocking.");
    True(session.Apply("new-app", Request(1, true, "B")).Applied, "A new app must remain able to own the profile.");
  }

  private static KeyLimiterUpdate Request(long revision, bool enabled, params string[] keys) =>
    new()
    {
      SchemaVersion = 2,
      Source = "impl-dm-note",
      ClientVersion = "0.1.0",
      SessionId = "test-session",
      Revision = revision,
      Enabled = enabled,
      Profile = new KeyLimiterProfile { Id = "4key", Name = "4key" },
      Keys = keys,
      ExcludedGhostKeys = Array.Empty<string>(),
    };

  private static void MapsTo(string input, KeyCode expected)
  {
    True(DmNoteKeyMapper.TryResolve(input, out ResolvedLimiterKey actual), input + " should map");
    Equal(expected, actual.UnityKey);
  }

  private static void MacModifierMapsTo(KeyCode key, ushort expected)
  {
    True(DmNoteKeyMapper.TryGetMacAsyncKey(key, out ushort actual), key + " should have a macOS async fallback");
    Equal(expected, actual);
  }

  private static void MapsAsyncTo(string input, KeyCode expectedKey, ushort expectedAsyncKey)
  {
    True(DmNoteKeyMapper.TryResolve(input, out ResolvedLimiterKey actual), input + " should map");
    Equal(expectedKey, actual.UnityKey);
    True(actual.HasAsyncKey, input + " should have an async key");
    Equal(expectedAsyncKey, actual.AsyncKey);
  }

  private static void Run(string name, Action test)
  {
    test();
    _passed++;
    Console.WriteLine("PASS " + name);
  }

  private static void True(bool value, string message)
  {
    if (!value)
      throw new InvalidOperationException(message);
  }

  private static void Equal<T>(T expected, T actual)
  {
    if (!Equals(expected, actual))
      throw new InvalidOperationException("Expected " + expected + ", got " + actual);
  }

  private static void Sequence(string[] expected, string[] actual)
  {
    Equal(string.Join("|", expected), string.Join("|", actual));
  }
}
