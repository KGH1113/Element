using System;
using System.IO;
using AdofaiIpc.Contracts;
using AdofaiIpc.Loader;

namespace Element.Infrastructure.ElementNote.Ipc;

/// <summary>
/// Element's IPC namespace, which ElementNote connects to. Its endpoints add their
/// commands, and get the peers and events, in the order they are given.
/// </summary>
internal sealed class ElementIpcChannel : IDisposable
{
  private const string Namespace = "element";
  private readonly string _modPath;
  private readonly string _version;
  private readonly Action<string> _log;
  private readonly IElementIpcEndpoint[] _endpoints;
  private IFeatureRegistration _ipc;

  public ElementIpcChannel(string modPath, string version, Action<string> log, params IElementIpcEndpoint[] endpoints)
  {
    _modPath = modPath;
    _version = version;
    _log = log ?? (_ => { });
    _endpoints = endpoints;
  }

  public void Enable()
  {
    if (_ipc != null)
      return;
    string mods = Directory
      .GetParent(Path.GetFullPath(_modPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)))
      .FullName;
    IIpcRuntime runtime = IpcLoader.GetRuntime(
      mods,
      new RuntimeRequirements(
        minimumRuntimeVersion: "2.1.0",
        requiredCapabilities: new[] { "commands", "game-thread", "snapshots" }
      )
    );
    _ipc = runtime.RegisterFeature(
      new FeatureDescription(
        Namespace,
        _version,
        protocolMajor: 1,
        displayName: "Element",
        allowedOrigins: new[]
        {
          "tauri://localhost",
          "http://tauri.localhost",
          "https://tauri.localhost",
          "http://localhost",
          "http://127.0.0.1",
        }
      ),
      builder =>
      {
        foreach (IElementIpcEndpoint endpoint in _endpoints)
          endpoint.AddCommands(builder);
        builder.OnClientAttached(OnPeerAttached);
        builder.OnClientDetached(OnPeerDetached);
        builder.OnEvent(OnPeerEvent);
      }
    );
    foreach (IElementIpcEndpoint endpoint in _endpoints)
      endpoint.Attach(_ipc);
    _ipc.MarkReady();
    _log("[IPC] WebSocket namespace ready: " + Namespace);
  }

  public void Dispose()
  {
    foreach (IElementIpcEndpoint endpoint in _endpoints)
      endpoint.Attach(null);
    if (_ipc == null)
      return;
    _ipc.Dispose();
    _ipc = null;
  }

  private void OnPeerAttached(IClientConnection peer)
  {
    foreach (IElementIpcEndpoint endpoint in _endpoints)
      endpoint.OnPeerAttached(peer);
  }

  private void OnPeerEvent(PeerEvent message)
  {
    foreach (IElementIpcEndpoint endpoint in _endpoints)
      endpoint.OnPeerEvent(message);
  }

  private void OnPeerDetached(IClientConnection peer)
  {
    foreach (IElementIpcEndpoint endpoint in _endpoints)
      endpoint.OnPeerDetached(peer);
  }
}
