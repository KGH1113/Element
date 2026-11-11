using AdofaiIpc.Contracts;

namespace Element.Infrastructure.ElementNote.Ipc;

/// <summary>One part of what Element offers ElementNote over its IPC channel.</summary>
internal interface IElementIpcEndpoint
{
  /// <summary>Adds the commands this part answers. Once, while the channel registers.</summary>
  void AddCommands(IFeatureBuilder builder);

  /// <summary>The channel is open; null once it closed. Sends go through it.</summary>
  void Attach(IFeatureRegistration ipc);

  void OnPeerAttached(IClientConnection peer);

  /// <summary>IPC thread.</summary>
  void OnPeerEvent(PeerEvent message);

  void OnPeerDetached(IClientConnection peer);
}
