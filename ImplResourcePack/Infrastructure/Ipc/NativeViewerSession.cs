using System;
using System.Linq;
using AdofaiIpc.Contracts;
using ImplResourcePack.Domain.DmNote;
using Newtonsoft.Json;

namespace ImplResourcePack.Infrastructure.Ipc;

/// <summary>What the main thread must do to the native host after IPC traffic.</summary>
internal readonly struct NativeViewerWork
{
  public readonly NativeViewerSurface[] Surfaces;
  public readonly bool ResetHost;
  public readonly bool HideHost;

  public NativeViewerWork(NativeViewerSurface[] surfaces, bool resetHost, bool hideHost)
  {
    Surfaces = surfaces;
    ResetHost = resetHost;
    HideHost = hideHost;
  }
}

/// <summary>
/// The ImplDmNote describe session, free of Unity and AppKit so it can be tested.
/// IPC callbacks arrive on a worker thread; every member locks.
/// </summary>
internal sealed class NativeViewerSession
{
  public const string DescribeCommand = "mirror.native.describe";
  private static readonly TimeSpan PendingTimeout = TimeSpan.FromSeconds(10);
  private static readonly TimeSpan PollWithoutPush = TimeSpan.FromSeconds(2);

  // Apps that push descriptor changes are still asked occasionally, in case an
  // event was lost while the connection recovered.
  private static readonly TimeSpan PollWithPush = TimeSpan.FromSeconds(30);

  private readonly object _gate = new();
  private readonly bool _supported;
  private string _peerId;
  private bool _pushSupported;
  private string _sessionId;
  private string _pendingId;
  private DateTime _pendingAt;
  private DateTime _nextRequestAt;
  private NativeViewerSurface[] _surfaces;
  private bool _active;
  private bool _closed;
  private bool _reset;
  private bool _hide;
  private NativeViewerStatus _status;

  public NativeViewerSession(bool supported)
  {
    _supported = supported;
    _status = supported ? NativeViewerStatus.WaitingForApp : NativeViewerStatus.UnsupportedPlatform;
  }

  public NativeViewerStatus Status
  {
    get
    {
      lock (_gate)
        return _status;
    }
  }

  public bool PushSupported
  {
    get
    {
      lock (_gate)
        return _pushSupported;
    }
  }

  internal string SessionId
  {
    get
    {
      lock (_gate)
        return _sessionId;
    }
  }

  public void SetActive(bool active, DateTime now)
  {
    lock (_gate)
    {
      if (_closed || _active == active)
        return;
      _active = active;
      _sessionId = active ? Guid.NewGuid().ToString("N") : null;
      _pendingId = null;
      _surfaces = null;
      _nextRequestAt = now;
      // Viewers live only while active; a new session starts with fresh pages.
      _reset = true;
      if (active && _supported)
        _status = _peerId == null ? NativeViewerStatus.WaitingForApp : NativeViewerStatus.Connecting;
    }
  }

  public void Close()
  {
    lock (_gate)
    {
      _closed = true;
      _active = false;
      _surfaces = null;
    }
  }

  public void OnPeerEvent(PeerEvent message, DateTime now)
  {
    lock (_gate)
    {
      if (_closed)
        return;
      switch (message.Name)
      {
        case "mirror.available":
          OnAvailable(message, now);
          return;
        // Pushed changes are uncorrelated; match them by peer and session.
        case "mirror.native.changed":
          if (IsCurrentPush(message))
            AcceptSurfaces(message.Json, now);
          return;
        case "mirror.native.unavailable":
          if (IsCurrentPush(message))
            AcceptFailure(Read<FailurePayload>(message.Json)?.Code, now);
          return;
      }
      if (!_active || message.PeerId != _peerId || message.CorrelationId == null || message.CorrelationId != _pendingId)
        return;
      if (message.Name != "mirror.native.ready" && message.Name != "mirror.failed")
        return;
      _pendingId = null;
      if (message.Name == "mirror.failed")
        AcceptFailure(Read<FailurePayload>(message.Json)?.Code, now);
      else
        AcceptSurfaces(message.Json, now);
    }
  }

  public void OnPeerDisconnected(string peerId)
  {
    lock (_gate)
    {
      if (peerId != _peerId)
        return;
      _peerId = null;
      _pushSupported = false;
      _pendingId = null;
      _surfaces = null;
      _reset = true;
      if (_supported)
        _status = NativeViewerStatus.WaitingForApp;
    }
  }

  /// <summary>Takes pending host work; call once per frame on the main thread.</summary>
  public NativeViewerWork TakeWork()
  {
    lock (_gate)
    {
      var work = new NativeViewerWork(_surfaces, _reset, _hide);
      _surfaces = null;
      _reset = _hide = false;
      return work;
    }
  }

  /// <summary>
  /// Sends a describe request when one is due. The send runs under the lock so a
  /// fast reply cannot arrive before its correlation id is recorded.
  /// </summary>
  public Exception RequestIfDue(DateTime now, Func<string, string, string> send)
  {
    lock (_gate)
    {
      if (!_active || _peerId == null || send == null || now < _nextRequestAt)
        return null;
      if (_pendingId != null && now - _pendingAt < PendingTimeout)
        return null;
      try
      {
        _pendingId = send(_peerId, JsonConvert.SerializeObject(new { sessionId = _sessionId }));
        _pendingAt = now;
        _nextRequestAt = now + Interval;
        return null;
      }
      catch (Exception error)
      {
        _pendingId = null;
        _nextRequestAt = now + PollWithoutPush;
        _status = NativeViewerStatus.Reconnecting;
        return error;
      }
    }
  }

  /// <summary>Records what the main thread observed while applying host work.</summary>
  public void Report(NativeViewerStatus status)
  {
    lock (_gate)
      _status = status;
  }

  private TimeSpan Interval => _pushSupported ? PollWithPush : PollWithoutPush;

  private void OnAvailable(PeerEvent message, DateTime now)
  {
    AvailabilityPayload availability = Read<AvailabilityPayload>(message.Json);
    // A malformed capability advertisement cannot select the native provider.
    if (availability?.NativeProtocolVersion != 1 || (_peerId != null && _peerId != message.PeerId))
      return;
    _peerId = message.PeerId;
    _pushSupported = availability.NativePush;
    _pendingId = null;
    _nextRequestAt = now;
    if (_active && _status == NativeViewerStatus.WaitingForApp)
      _status = NativeViewerStatus.Connecting;
  }

  private bool IsCurrentPush(PeerEvent message) =>
    _active
    && _sessionId != null
    && message.PeerId == _peerId
    && Read<FailurePayload>(message.Json)?.SessionId == _sessionId;

  private void AcceptSurfaces(string json, DateTime now)
  {
    ReplyPayload reply = Read<ReplyPayload>(json);
    if (reply == null)
    {
      ApplyFailure(NativeViewerFailure.Incompatible, now);
      return;
    }
    if (reply.SessionId != _sessionId)
      return;
    if (
      reply.Surfaces == null
      || reply.Surfaces.Length > 2
      || reply.Surfaces.Any(surface => surface == null || !surface.IsValid())
      || reply.Surfaces.Select(surface => surface.ViewerKind).Distinct().Count() != reply.Surfaces.Length
    )
    {
      ApplyFailure(NativeViewerFailure.Incompatible, now);
      return;
    }
    _surfaces = reply.Surfaces;
    _nextRequestAt = now + Interval;
  }

  private void AcceptFailure(string code, DateTime now) =>
    ApplyFailure(NativeViewerFailure.From(code, _pushSupported), now);

  private void ApplyFailure(NativeViewerFailure failure, DateTime now)
  {
    _status = failure.Status;
    _nextRequestAt = failure.StopRequests ? DateTime.MaxValue : now + failure.RetryAfter;
    if (failure.ResetHost)
    {
      _surfaces = null;
      _reset = true;
    }
    else if (failure.HideHost)
      _hide = true;
  }

  private static T Read<T>(string json)
    where T : class
  {
    try
    {
      return string.IsNullOrEmpty(json) ? null : JsonConvert.DeserializeObject<T>(json);
    }
    catch (JsonException)
    {
      return null;
    }
  }

  private sealed class ReplyPayload
  {
    public string SessionId { get; set; }
    public NativeViewerSurface[] Surfaces { get; set; }
  }

  private sealed class FailurePayload
  {
    public string SessionId { get; set; }
    public string Code { get; set; }
  }

  private sealed class AvailabilityPayload
  {
    public int? NativeProtocolVersion { get; set; }
    public bool NativePush { get; set; }
  }
}
