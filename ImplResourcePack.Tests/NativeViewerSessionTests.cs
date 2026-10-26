using System;
using System.Collections.Generic;
using AdofaiIpc.Contracts;
using ImplResourcePack.Domain.DmNote;
using ImplResourcePack.Infrastructure.Game.DmNote;
using ImplResourcePack.Infrastructure.Ipc;
using Newtonsoft.Json;

namespace ImplResourcePack.Tests;

internal static class NativeViewerSessionTests
{
  private const string App = "impl-dm-note";
  private static readonly DateTime Start = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

  public static void Run()
  {
    ClassifiesFailures();
    ComparesDescriptors();
    MapsNativeHostErrors();
    PollsUntilTheAppPushes();
    AcceptsPushesOnlyForTheCurrentSession();
    KeepsTheHostForTransientFailures();
    StopsOnIncompatibleReplies();
    DisconnectResetsTheHost();
  }

  private static void ClassifiesFailures()
  {
    foreach (string code in new[] { "mirror-busy", "obs-not-ready", "native-unavailable", "replay-render-active" })
      Check(!NativeViewerFailure.From(code, true).ResetHost, code + " is transient and must keep the pages loaded.");
    Check(NativeViewerFailure.From("replay-render-active", true).HideHost, "Replay rendering hides the viewers.");
    Check(NativeViewerFailure.From("obs-required", true).ResetHost, "Stopping OBS invalidates the page URLs.");
    Check(NativeViewerFailure.From("invalid-session", false).StopRequests, "An invalid session must stop polling.");
    Check(
      NativeViewerFailure.From("obs-required", true).RetryAfter
        > NativeViewerFailure.From("obs-required", false).RetryAfter,
      "Push-capable apps are polled rarely."
    );
  }

  private static void ComparesDescriptors()
  {
    NativeViewerSurface[] first = { Surface("hand", 860), Surface("foot", 320) };
    NativeViewerSurface[] reordered = { Surface("foot", 320), Surface("hand", 860) };
    NativeViewerSurface[] resized = { Surface("hand", 900), Surface("foot", 320) };
    Check(NativeViewerSurface.SameSet(first, reordered), "Descriptor order must not force a page update.");
    Check(!NativeViewerSurface.SameSet(first, resized), "A size change must update the host.");
    Check(
      !NativeViewerSurface.SameSet(first, new[] { Surface("hand", 860) }),
      "A removed viewer must update the host."
    );
  }

  private static void MapsNativeHostErrors()
  {
    Check(
      MacGameWebView.Classify("Native viewer renderer stopped. Viewers will reconnect automatically.")
        == NativeViewerStatus.Reconnecting,
      "A crashed page reconnects by itself."
    );
    Check(
      MacGameWebView.Classify("The Unity game window was not found. Return to the game and retry.")
        == NativeViewerStatus.WindowUnavailable,
      "A missing game window asks the player to return to it."
    );
    Check(
      MacGameWebView.Classify("Native viewer could not load its local source. Check ImplDmNote.")
        == NativeViewerStatus.PageUnavailable,
      "Page load errors suggest reloading OBS mode."
    );
  }

  private static void PollsUntilTheAppPushes()
  {
    var session = Active(out List<string> sent);
    session.OnPeerEvent(Event("mirror.available", new { nativeProtocolVersion = 1 }), Start);
    Check(session.RequestIfDue(Start, Send(sent)) == null && sent.Count == 1, "The first describe is sent at once.");
    Reply(session, "describe-1", Start);
    session.RequestIfDue(Start.AddSeconds(1), Send(sent));
    session.RequestIfDue(Start.AddSeconds(2.1), Send(sent));
    Check(sent.Count == 2, "Apps without push are described every two seconds.");

    var pushing = Active(out List<string> pushed);
    pushing.OnPeerEvent(Event("mirror.available", new { nativeProtocolVersion = 1, nativePush = true }), Start);
    pushing.RequestIfDue(Start, Send(pushed));
    Reply(pushing, "describe-1", Start);
    pushing.RequestIfDue(Start.AddSeconds(10), Send(pushed));
    Check(pushed.Count == 1 && pushing.PushSupported, "Push-capable apps are not polled every two seconds.");
    pushing.RequestIfDue(Start.AddSeconds(31), Send(pushed));
    Check(pushed.Count == 2, "A rare fallback describe recovers a lost push.");
  }

  private static void AcceptsPushesOnlyForTheCurrentSession()
  {
    var session = Connected(out _);
    session.TakeWork();
    session.OnPeerEvent(
      Event("mirror.native.changed", new { sessionId = "0123456789abcdef0123456789abcdef", surfaces = Surfaces() }),
      Start
    );
    Check(session.TakeWork().Surfaces == null, "A stale session's push must be ignored.");
    session.OnPeerEvent(
      Event("mirror.native.changed", new { sessionId = session.SessionId, surfaces = Surfaces() }, peer: "other"),
      Start
    );
    Check(session.TakeWork().Surfaces == null, "Another peer's push must be ignored.");
    session.OnPeerEvent(
      Event("mirror.native.ready", new { sessionId = session.SessionId, surfaces = Surfaces() }),
      Start
    );
    Check(session.TakeWork().Surfaces == null, "An uncorrelated ready reply must be ignored.");
    session.OnPeerEvent(
      Event("mirror.native.changed", new { sessionId = session.SessionId, surfaces = Surfaces() }),
      Start
    );
    Check(session.TakeWork().Surfaces?.Length == 1, "The current session's push updates the viewers.");
  }

  private static void KeepsTheHostForTransientFailures()
  {
    var session = Connected(out List<string> sent);
    session.TakeWork();
    session.OnPeerEvent(
      Event("mirror.native.unavailable", new { sessionId = session.SessionId, code = "mirror-busy" }),
      Start
    );
    NativeViewerWork work = session.TakeWork();
    Check(!work.ResetHost && !work.HideHost, "A busy app must not reload the pages.");
    Check(session.Status == NativeViewerStatus.Preparing, "A busy app shows the preparing message.");
    session.OnPeerEvent(
      Event("mirror.native.unavailable", new { sessionId = session.SessionId, code = "replay-render-active" }),
      Start
    );
    Check(session.TakeWork().HideHost, "Replay rendering hides the pages without reloading them.");
    session.OnPeerEvent(
      Event("mirror.native.unavailable", new { sessionId = session.SessionId, code = "obs-required" }),
      Start
    );
    Check(session.TakeWork().ResetHost, "Turning OBS mode off releases the pages.");
    Check(session.Status == NativeViewerStatus.ObsModeOff, "The player is asked to turn OBS mode on.");
  }

  private static void StopsOnIncompatibleReplies()
  {
    var session = Active(out List<string> sent);
    session.OnPeerEvent(Event("mirror.available", new { nativeProtocolVersion = 1 }), Start);
    session.RequestIfDue(Start, Send(sent));
    session.OnPeerEvent(Event("mirror.native.ready", "not json", correlation: "describe-1"), Start);
    Check(session.Status == NativeViewerStatus.Incompatible, "A malformed reply asks for an update.");
    session.RequestIfDue(Start.AddMinutes(5), Send(sent));
    Check(sent.Count == 1, "An incompatible app is not polled again in this session.");
  }

  private static void DisconnectResetsTheHost()
  {
    var session = Connected(out _);
    session.TakeWork();
    session.OnPeerDisconnected(App);
    Check(session.TakeWork().ResetHost, "A disconnected app's pages are released.");
    Check(session.Status == NativeViewerStatus.WaitingForApp, "The player is asked to open the app.");
    var unsupported = new NativeViewerSession(false);
    unsupported.SetActive(true, Start);
    unsupported.OnPeerEvent(Event("mirror.available", new { nativeProtocolVersion = 1 }), Start);
    Check(unsupported.Status == NativeViewerStatus.UnsupportedPlatform, "Other platforms explain the macOS limit.");
  }

  private static NativeViewerSession Active(out List<string> sent)
  {
    sent = new List<string>();
    var session = new NativeViewerSession(true);
    session.SetActive(true, Start);
    return session;
  }

  private static NativeViewerSession Connected(out List<string> sent)
  {
    var session = Active(out sent);
    session.OnPeerEvent(Event("mirror.available", new { nativeProtocolVersion = 1, nativePush = true }), Start);
    session.RequestIfDue(Start, Send(sent));
    Reply(session, "describe-1", Start);
    return session;
  }

  private static void Reply(NativeViewerSession session, string correlation, DateTime now) =>
    session.OnPeerEvent(
      Event(
        "mirror.native.ready",
        new { sessionId = session.SessionId, surfaces = Surfaces() },
        correlation: correlation
      ),
      now
    );

  private static Func<string, string, string> Send(List<string> sent) =>
    (peer, json) =>
    {
      sent.Add(json);
      return "describe-" + sent.Count;
    };

  private static PeerEvent Event(string name, object payload, string peer = App, string correlation = null) =>
    new(
      peer,
      "connection",
      name,
      Guid.NewGuid().ToString("N"),
      correlation,
      payload as string ?? JsonConvert.SerializeObject(payload)
    );

  private static object[] Surfaces() =>
    new object[]
    {
      new
      {
        viewerKind = "hand",
        url = "http://127.0.0.1:34891/?viewer=hand",
        width = 860,
        height = 320,
      },
    };

  private static NativeViewerSurface Surface(string kind, int width) =>
    new()
    {
      ViewerKind = kind,
      Url = "http://127.0.0.1:34891/?viewer=" + kind,
      Width = width,
      Height = 320,
    };

  private static void Check(bool value, string message)
  {
    if (!value)
      throw new InvalidOperationException(message);
  }
}
