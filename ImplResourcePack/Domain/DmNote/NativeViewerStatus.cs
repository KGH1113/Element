using System;

namespace ImplResourcePack.Domain.DmNote;

/// <summary>Why a native key viewer is or is not showing, in user terms.</summary>
internal enum NativeViewerStatus
{
  WaitingForApp,
  Connecting,
  Attached,
  NoViewerTab,
  ObsModeOff,
  HiddenInApp,
  Preparing,
  ReplayRendering,
  Reconnecting,
  Incompatible,
  PageUnavailable,
  WindowUnavailable,
  ComponentMissing,
  WebViewRuntimeMissing,
  UnsupportedSystem,
  UnsupportedPlatform,
}

/// <summary>How the game should react to a <c>mirror.failed</c> code from ImplDmNote.</summary>
internal readonly struct NativeViewerFailure
{
  public readonly NativeViewerStatus Status;

  /// <summary>Destroy the WebKit host. Only dead page URLs or a broken session need it.</summary>
  public readonly bool ResetHost;

  /// <summary>Keep the host but hide it until the app reports surfaces again.</summary>
  public readonly bool HideHost;

  /// <summary>Stop describing until the session restarts.</summary>
  public readonly bool StopRequests;

  public readonly TimeSpan RetryAfter;

  private NativeViewerFailure(
    NativeViewerStatus status,
    bool resetHost,
    bool hideHost,
    bool stopRequests,
    TimeSpan retryAfter
  )
  {
    Status = status;
    ResetHost = resetHost;
    HideHost = hideHost;
    StopRequests = stopRequests;
    RetryAfter = retryAfter;
  }

  // Transient codes keep the WebKit host: recreating it reloads both pages and
  // starts a new web content process for a condition that clears in seconds.
  public static NativeViewerFailure From(string code, bool pushSupported)
  {
    TimeSpan idle = pushSupported ? TimeSpan.FromSeconds(30) : TimeSpan.FromSeconds(2);
    return code switch
    {
      "mirror-busy" => new(NativeViewerStatus.Preparing, false, false, false, TimeSpan.FromSeconds(1)),
      "obs-not-ready" => new(NativeViewerStatus.Preparing, false, false, false, TimeSpan.FromSeconds(1)),
      "native-unavailable" => new(NativeViewerStatus.Preparing, false, false, false, TimeSpan.FromSeconds(2)),
      "replay-render-active" => new(NativeViewerStatus.ReplayRendering, false, true, false, idle),
      "obs-required" => new(NativeViewerStatus.ObsModeOff, true, false, false, idle),
      "in-game-disabled" => new(NativeViewerStatus.HiddenInApp, true, false, false, idle),
      "invalid-session" => new(NativeViewerStatus.Incompatible, true, false, true, TimeSpan.Zero),
      _ => new(NativeViewerStatus.Preparing, false, false, false, idle),
    };
  }

  public static NativeViewerFailure Incompatible =>
    new(NativeViewerStatus.Incompatible, true, false, true, TimeSpan.Zero);
}
