using System;
using AdofaiIpc.Contracts;
using Element.Infrastructure.ElementNote.Ipc;

namespace Element.Infrastructure.ElementNote.Viewers;

/// <summary>
/// The native viewers' part of Element's IPC channel: hands ElementNote's events
/// to the describe session and sends what the session and the editor ask. It
/// keeps no viewer state of its own.
/// </summary>
internal sealed class NativeViewerIpcEndpoint : IElementIpcEndpoint
{
  private readonly NativeViewerSession _session;
  private readonly Func<string, string, string> _requestDescriptor;
  private IFeatureRegistration _ipc;

  public NativeViewerIpcEndpoint(NativeViewerSession session)
  {
    _session = session ?? throw new ArgumentNullException(nameof(session));
    // A delegate made once: a lambda per request would allocate every frame.
    _requestDescriptor = (peer, json) => _ipc.SendCommandToPeer(peer, NativeViewerSession.DescribeCommand, json);
  }

  /// <summary>The channel is open, so requests can be sent.</summary>
  public bool Attached => _ipc != null;

  public void AddCommands(IFeatureBuilder builder) { }

  public void Attach(IFeatureRegistration ipc) => _ipc = ipc;

  public void OnPeerAttached(IClientConnection peer) { }

  // IPC thread: the session only records state; the main thread applies it.
  public void OnPeerEvent(PeerEvent message) => _session.OnPeerEvent(message, DateTime.UtcNow);

  public void OnPeerDetached(IClientConnection peer) => _session.OnPeerDisconnected(peer.PeerId);

  /// <summary>Asks ElementNote for its viewers when the session says it is time. Unity main thread, every frame.</summary>
  public Exception RequestDescriptorIfDue() =>
    _session.RequestIfDue(DateTime.UtcNow, _ipc == null ? null : _requestDescriptor);

  /// <summary>Shows the tab of <paramref name="viewerKind"/> in ElementNote; false once the channel closed.</summary>
  public bool SendEdit(string peer, string viewerKind)
  {
    IFeatureRegistration ipc = _ipc;
    if (ipc == null)
      return false;
    ipc.SendCommandToPeer(peer, NativeViewerSession.EditCommand, NativeViewerSession.EditPayload(viewerKind));
    return true;
  }
}
