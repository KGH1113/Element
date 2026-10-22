using System;
using System.Linq;
using AdofaiIpc.Contracts;
using ImplResourcePack.Domain.DmNote;
using ImplResourcePack.Infrastructure.Game.DmNote;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ImplResourcePack.Infrastructure.Ipc;

internal sealed class DmNoteNativeIpcFeature : IDisposable
{
  private readonly object _gate = new();
  private IFeatureRegistration _ipc;
  private MacGameWebView _host;
  private string _peerId;
  private string _sessionId;
  private string _pendingId;
  private DateTime _pendingAt;
  private DateTime _nextRequestAt;
  private DateTime _nextErrorCheckAt;
  private NativeViewerSurface[] _surfaces;
  private bool _active;
  private bool _disposed;
  private bool _reset;
  private string _status = "Enable OBS mode in ImplDmNote, then start a level or show the preview.";

  private sealed class Reply
  {
    public string SessionId { get; set; }
    public NativeViewerSurface[] Surfaces { get; set; }
  }

  public string Status
  {
    get
    {
      lock (_gate)
        return _status;
    }
  }

  public void Attach(IFeatureRegistration ipc) => _ipc = ipc;

  public void SetActive(bool active)
  {
    lock (_gate)
    {
      if (_disposed || _active == active)
        return;
      _active = active;
      _sessionId = active ? Guid.NewGuid().ToString("N") : null;
      _pendingId = null;
      _surfaces = null;
      _nextRequestAt = DateTime.UtcNow;
      _reset = true;
      if (active)
        _status =
          _peerId == null
            ? "Waiting for ImplDmNote. Open the updated app and enable OBS mode."
            : "Connecting to OBS viewers...";
    }
  }

  public void OnPeerEvent(PeerEvent message)
  {
    lock (_gate)
    {
      if (_disposed)
        return;
      if (message.Name == "mirror.available")
      {
        try
        {
          if ((int?)JObject.Parse(message.Json)?["nativeProtocolVersion"] == 1 && _peerId == null)
          {
            _peerId = message.PeerId;
            _nextRequestAt = DateTime.UtcNow;
          }
        }
        catch (JsonException)
        {
          // A malformed capability advertisement cannot select the native provider.
        }
        return;
      }
      if (!_active || message.PeerId != _peerId || message.CorrelationId != _pendingId)
        return;
      if (message.Name != "mirror.native.ready" && message.Name != "mirror.failed")
        return;
      _pendingId = null;
      _nextRequestAt = DateTime.UtcNow.AddSeconds(2);
      if (message.Name == "mirror.failed")
      {
        _reset = true;
        string code = null;
        try
        {
          code = (string)JObject.Parse(message.Json)?["code"];
        }
        catch (JsonException) { }
        _status = code switch
        {
          "replay-render-active" =>
            "Replay rendering is using ImplDmNote. Native viewers will reconnect when it finishes.",
          "obs-not-ready" => "The OBS viewer service is starting. Native viewers will reconnect automatically.",
          "mirror-busy" => "ImplDmNote is finishing another viewer request. Retrying shortly.",
          _ =>
            "OBS viewers are unavailable. Enable OBS mode in ImplDmNote; native viewers will reconnect automatically.",
        };
        return;
      }
      try
      {
        Reply reply = JsonConvert.DeserializeObject<Reply>(message.Json);
        if (reply?.SessionId != _sessionId)
          return;
        if (
          reply.Surfaces == null
          || reply.Surfaces.Length > 2
          || reply.Surfaces.Any(surface => surface == null || !surface.IsValid())
          || reply.Surfaces.Select(surface => surface.ViewerKind).Distinct().Count() != reply.Surfaces.Length
        )
          throw new JsonSerializationException();
        _surfaces = reply.Surfaces;
      }
      catch (JsonException)
      {
        _reset = true;
        _status = "The app sent an incompatible native viewer description. Update both the app and mod.";
      }
    }
  }

  public void OnPeerDisconnected(IClientConnection peer)
  {
    lock (_gate)
    {
      if (peer.PeerId != _peerId)
        return;
      _peerId = null;
      _pendingId = null;
      _surfaces = null;
      _reset = true;
      _status = "ImplDmNote disconnected. Reopen the app and enable OBS mode.";
    }
  }

  // Called on Unity's main thread; IPC callbacks never access AppKit.
  public void Tick()
  {
    NativeViewerSurface[] surfaces;
    bool reset;
    lock (_gate)
    {
      if (_disposed)
        return;
      reset = _reset;
      _reset = false;
      surfaces = _surfaces;
      _surfaces = null;
    }
    if (reset)
    {
      _host?.Dispose();
      _host = null;
    }
    if (surfaces != null)
    {
      try
      {
        _host ??= new MacGameWebView(Main.Instance.Path);
        _host.Apply(surfaces);
        lock (_gate)
          _status =
            surfaces.Length == 0
              ? "No viewers selected. Open a hand or foot viewer before enabling OBS mode."
              : "Native viewers attached. Check the Discord / OBS capture preview.";
      }
      catch (Exception error)
      {
        _host?.Dispose();
        _host = null;
        lock (_gate)
          _status =
            error is InvalidOperationException
              ? error.Message
              : "Native viewers could not open. Reinstall the mod and restart ADOFAI.";
      }
    }
    if (_host != null && DateTime.UtcNow >= _nextErrorCheckAt)
    {
      _nextErrorCheckAt = DateTime.UtcNow.AddMilliseconds(500);
      string error = _host.LoadError;
      if (!string.IsNullOrWhiteSpace(error))
      {
        lock (_gate)
          _status = error;
      }
    }
    lock (_gate)
    {
      if (!_active || _peerId == null || _ipc == null || DateTime.UtcNow < _nextRequestAt)
        return;
      if (_pendingId != null && DateTime.UtcNow - _pendingAt < TimeSpan.FromSeconds(10))
        return;
      try
      {
        _pendingId = _ipc.SendCommandToPeer(
          _peerId,
          "mirror.native.describe",
          JsonConvert.SerializeObject(new { sessionId = _sessionId })
        );
        _pendingAt = DateTime.UtcNow;
        _nextRequestAt = _pendingAt.AddSeconds(2);
      }
      catch
      {
        _nextRequestAt = DateTime.UtcNow.AddSeconds(2);
        _status = "Could not reach ImplDmNote. Check that the updated app is connected to the game.";
      }
    }
  }

  public void Dispose()
  {
    lock (_gate)
    {
      if (_disposed)
        return;
      _disposed = true;
      _active = false;
      _ipc = null;
      _surfaces = null;
    }
    _host?.Dispose();
    _host = null;
  }
}
