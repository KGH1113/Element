using System;
using System.Threading;
using Element.Application.KeyLimiter;
using Element.Infrastructure.ElementNote.Keys;
using Element.Tests.Support;
using UnityEngine;
using static Element.Tests.Support.Assert;

namespace Element.Tests.Application.KeyLimiter;

/// <summary>The key limiter applies ElementNote's key profile, owned by one app at a time.</summary>
internal static class KeyLimiterTests
{
  [Suite("allows ANSI and ISO backslash input")]
  public static void AllowsBackslashInput()
  {
    foreach (string name in new[] { "BACKSLASH", "\\" })
    {
      KeyLimiterService service = new(new ElementNoteKeyResolver());
      service.Apply(Request(1, true, name));
      Check(service.Current.Allows(KeyCode.Backslash), "backslash should be allowed");
      if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.OSX))
      {
        Check(service.Current.AllowsAsync(0x31), "ANSI backslash should be allowed");
        Check(service.Current.AllowsAsync(0x64), "ISO backslash should be allowed");
        Check(!service.Current.AllowsAsync(0x38), "forward slash must remain blocked");
        service.Apply(Request(2, true, "A"));
        Check(!service.Current.AllowsAsync(0x31), "removed ANSI backslash must be blocked");
        Check(!service.Current.AllowsAsync(0x64), "removed ISO backslash must be blocked");
      }
    }
  }

  [Suite("applies supported keys and reports unsupported keys")]
  public static void AppliesSupportedSubset()
  {
    KeyLimiterService service = new(new ElementNoteKeyResolver());
    KeyLimiterApplyResult response = service.Apply(Request(1, true, "A", "FN", "DOT"));
    Check(response.Ok && response.Applied && response.Enabled, "request should be applied");
    Sequence(new[] { "A", "DOT" }, response.SupportedKeys);
    Sequence(new[] { "FN" }, response.UnsupportedKeys);
    Check(service.Current.Allows(KeyCode.A), "A should be allowed");
    Check(!service.Current.Allows(KeyCode.B), "B should be blocked");
    // ADOFAI keeps Escape out of main input itself, so the profile never needs it.
    Check(!service.Current.Allows(KeyCode.Escape), "Escape is not part of the profile");
  }

  [Suite("handles duplicate and stale revisions")]
  public static void HandlesRevisionOrdering()
  {
    KeyLimiterService service = new(new ElementNoteKeyResolver());
    service.Apply(Request(2, true, "A"));
    Equal("duplicate", service.Apply(Request(2, true, "S")).Reason);
    Equal("stale_revision", service.Apply(Request(1, true, "S")).Reason);
    Check(service.Current.Allows(KeyCode.A), "stale requests must not replace state");
  }

  [Suite("profile ownership and disconnect restore")]
  public static void HandlesProfileOwnership()
  {
    KeyLimiterService profiles = new(new ElementNoteKeyResolver());
    KeyLimiterSessionController session = new(profiles);
    Check(session.Apply("first-app", Request(1, true, "A")).Applied, "First app must own the profile.");
    Check(
      session.Apply("second-app", Request(2, true, "B")).Reason == "profile_owned_by_another_app",
      "A second app cannot replace the profile."
    );
    Check(
      !session.PeerDisconnected("second-app") && profiles.Current.Enabled,
      "An unrelated disconnect must preserve the owner."
    );
    Check(
      session.PeerDisconnected("first-app") && !profiles.Current.Enabled,
      "Owner disconnect must release the key limiter."
    );
    Check(
      session.Apply("second-app", Request(1, true, "B")).Applied,
      "A new connection can become owner after release."
    );
    KeyLimiterUpdate invalid = Request(2, true, "A");
    invalid.SchemaVersion = 1;
    Check(
      session.Apply("second-app", invalid).Reason == "unsupported_schema",
      "The old profile schema must be rejected."
    );
    session.Clear();
    Check(!profiles.Current.Enabled, "Mod shutdown must release the profile.");
  }

  [Suite("canceled queued profile cannot regain ownership")]
  public static void RejectsCanceledProfile()
  {
    KeyLimiterService profiles = new(new ElementNoteKeyResolver());
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
    Check(rejected && !profiles.Current.Enabled, "Disconnected queued work must not re-enable input blocking.");
    Check(session.Apply("new-app", Request(1, true, "B")).Applied, "A new app must remain able to own the profile.");
  }

  [Suite("disables when no requested key is supported")]
  public static void DisablesUnsupportedOnlyProfile()
  {
    KeyLimiterService service = new(new ElementNoteKeyResolver());
    KeyLimiterApplyResult response = service.Apply(Request(1, true, "FN"));
    Check(response.Applied && !response.Enabled, "unsupported-only profile must fail open");
  }

  private static KeyLimiterUpdate Request(long revision, bool enabled, params string[] keys) =>
    new()
    {
      SchemaVersion = 2,
      Source = "element-note",
      ClientVersion = "0.1.0",
      SessionId = "test-session",
      Revision = revision,
      Enabled = enabled,
      Profile = new KeyLimiterProfile { Id = "4key", Name = "4key" },
      Keys = keys,
      ExcludedGhostKeys = Array.Empty<string>(),
    };
}
