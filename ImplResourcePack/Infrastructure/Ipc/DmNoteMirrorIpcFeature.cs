using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AdofaiIpc;
using ImplResourcePack.Presentation.Overlay.DmNote;

namespace ImplResourcePack.Infrastructure.Ipc;

internal sealed class DmNoteMirrorIpcFeature : IDisposable
{
  private const int FrameIntervalMs = 50;
  private const long MaxFrameBytes = 64L * 1024 * 1024;
  private readonly object _gate = new();
  private readonly DmNoteMirrorView _view;
  private AdofaiIpcNamespace _ipc;
  private string _peerId;
  private string _sessionId;
  private string _pendingId;
  private DateTime _pendingAt;
  private DateTime _nextRequestAt;
  private Task<LoadedFrame[]> _readTask;
  private bool _active;
  private bool _disposed;
  private bool _hideView;
  private string _warning;
  private string _lastWarning;

  private sealed class FrameReply
  {
    public FrameReply() { }

    public string SessionId { get; set; }
    public FrameDescriptor[] Frames { get; set; }
  }

  private sealed class FrameDescriptor
  {
    public FrameDescriptor() { }

    public string ViewerKind { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public string Format { get; set; }
    public string Path { get; set; }
  }

  private sealed class LoadedFrame
  {
    public FrameDescriptor Descriptor;
    public byte[] Pixels;
  }

  public DmNoteMirrorIpcFeature(DmNoteMirrorView view) => _view = view;

  public void Attach(AdofaiIpcNamespace ipc) => _ipc = ipc;

  public void SetActive(bool active)
  {
    string peer;
    string oldSession;
    lock (_gate)
    {
      if (_disposed || _active == active)
        return;
      _active = active;
      peer = _peerId;
      oldSession = _sessionId;
      _sessionId = active ? Guid.NewGuid().ToString("N") : null;
      _pendingId = null;
      _readTask = null;
      _nextRequestAt = DateTime.UtcNow;
    }
    _view.SetVisible(active);
    if (active)
      _view.HideMissing(false, false);
    if (!active && peer != null && oldSession != null)
      TrySendEnd(peer, oldSession);
  }

  public void OnPeerEvent(IpcPeerEvent message)
  {
    if (message.Name == "mirror.available")
    {
      if ((int?)message.Payload?["protocolVersion"] != 1)
        return;
      lock (_gate)
      {
        if (_peerId == null && !_disposed)
        {
          _peerId = message.PeerId;
          _sessionId = _active ? Guid.NewGuid().ToString("N") : null;
          _nextRequestAt = DateTime.UtcNow;
        }
      }
      return;
    }

    lock (_gate)
    {
      if (_disposed || !_active || message.PeerId != _peerId || message.CorrelationId != _pendingId)
        return;
      _pendingId = null;
      if (message.Name == "mirror.frame.ready")
      {
        _lastWarning = null;
        FrameReply reply;
        try
        {
          reply = message.Payload?.ToObject<FrameReply>();
        }
        catch
        {
          _nextRequestAt = DateTime.UtcNow.AddSeconds(2);
          return;
        }
        if (reply?.SessionId != _sessionId || reply.Frames == null)
          return;
        _readTask = Task.Run(() =>
          reply
            .Frames.Where(frame => frame != null && (frame.ViewerKind == "hand" || frame.ViewerKind == "foot"))
            .Select(frame => new LoadedFrame { Descriptor = frame, Pixels = ReadFrame(frame) })
            .ToArray()
        );
      }
      else
      {
        string detail = message.Payload?["message"]?.ToString();
        if (detail?.Length > 256)
          detail = detail.Substring(0, 256);
        if (!string.IsNullOrWhiteSpace(detail) && detail != _lastWarning)
        {
          _warning = detail;
          _lastWarning = detail;
        }
        _nextRequestAt = DateTime.UtcNow.AddSeconds(2);
      }
    }
  }

  public void OnPeerDisconnected(IpcPeerInfo peer)
  {
    lock (_gate)
    {
      if (peer.PeerId != _peerId)
        return;
      _peerId = null;
      _pendingId = null;
      _readTask = null;
      _sessionId = _active ? Guid.NewGuid().ToString("N") : null;
      _hideView = true;
    }
  }

  public void Tick()
  {
    Task<LoadedFrame[]> completed = null;
    bool hide;
    string warning;
    lock (_gate)
    {
      if (_disposed)
        return;
      hide = _hideView;
      _hideView = false;
      warning = _warning;
      _warning = null;
      if (_readTask?.IsCompleted == true)
      {
        completed = _readTask;
        _readTask = null;
        _nextRequestAt = DateTime.UtcNow.AddMilliseconds(FrameIntervalMs);
      }
    }
    if (hide)
      _view.HideMissing(false, false);
    if (warning != null)
      Main.Instance.LogWarning("[DmNote mirror] " + warning);
    if (completed != null)
    {
      try
      {
        LoadedFrame[] frames = completed.GetAwaiter().GetResult();
        _view.HideMissing(
          frames.Any(frame => frame.Descriptor.ViewerKind == "hand"),
          frames.Any(frame => frame.Descriptor.ViewerKind == "foot")
        );
        foreach (LoadedFrame frame in frames)
          _view.Apply(
            frame.Descriptor.ViewerKind,
            frame.Descriptor.Width,
            frame.Descriptor.Height,
            frame.Descriptor.Format,
            frame.Pixels
          );
      }
      catch (Exception error)
      {
        Main.Instance.LogWarning("[DmNote mirror] Could not load a viewer frame: " + error.Message);
        lock (_gate)
          _nextRequestAt = DateTime.UtcNow.AddSeconds(2);
      }
    }

    lock (_gate)
    {
      if (!_active || _peerId == null || _readTask != null || DateTime.UtcNow < _nextRequestAt)
        return;
      if (_pendingId != null)
      {
        if (DateTime.UtcNow - _pendingAt < TimeSpan.FromSeconds(20))
          return;
        _pendingId = null;
        _sessionId = Guid.NewGuid().ToString("N");
      }
      try
      {
        _pendingId = _ipc.SendCommandToPeer(_peerId, "mirror.frame", new { sessionId = _sessionId });
        _pendingAt = DateTime.UtcNow;
        _nextRequestAt = _pendingAt.AddMilliseconds(FrameIntervalMs);
      }
      catch (Exception error)
      {
        _nextRequestAt = DateTime.UtcNow.AddSeconds(2);
        Main.Instance.LogWarning("[DmNote mirror] Could not request a viewer frame: " + error.Message);
      }
    }
  }

  public void Dispose()
  {
    SetActive(false);
    lock (_gate)
    {
      _disposed = true;
      _ipc = null;
    }
    _view.Dispose();
  }

  private void TrySendEnd(string peer, string session)
  {
    try
    {
      _ipc?.SendCommandToPeer(peer, "mirror.end", new { sessionId = session });
    }
    catch
    {
      // The peer may already have disconnected; its coordinator clears the session.
    }
  }

  private static byte[] ReadFrame(FrameDescriptor frame)
  {
    if (
      frame.Width <= 0
      || frame.Height <= 0
      || frame.Width > 4096
      || frame.Height > 4096
      || (frame.Format != "rgba" && frame.Format != "png")
      || string.IsNullOrEmpty(frame.Path)
      || !Path.IsPathRooted(frame.Path)
    )
      throw new InvalidDataException("The viewer frame description is invalid.");

    long length = new FileInfo(frame.Path).Length;
    if (
      length <= 0
      || length > MaxFrameBytes
      || (frame.Format == "rgba" && length != (long)frame.Width * frame.Height * 4)
    )
      throw new InvalidDataException("The viewer frame is too large or has an invalid size.");
    return File.ReadAllBytes(frame.Path);
  }
}
