using System;
using AdofaiIpc;
using AdofaiIpc.Core;
using ImplResourcePack.Application.KeyLimiter;
using ImplResourcePack.Domain.Input;

namespace ImplResourcePack.Infrastructure.Ipc;

internal sealed class KeyLimiterIpcFeature : IDisposable
{
  private const string Namespace = "impl-resourcepack";
  private readonly KeyLimiterSessionController _session;
  private DmNoteMirrorIpcFeature _mirror;
  private AdofaiIpcNamespace _ipc;

  public KeyLimiterIpcFeature(KeyLimiterSessionController session) => _session = session;

  public void AttachMirror(DmNoteMirrorIpcFeature mirror) => _mirror = mirror;

  public void Enable()
  {
    if (_ipc != null)
      return;
    _ipc = AdofaiIpc.AdofaiIpc.RegisterNamespace(
      Namespace,
      new IpcNamespaceInfo
      {
        DisplayName = "ImplResourcePack",
        Version = Main.Instance.Version,
        AllowedOrigins = new[]
        {
          "tauri://localhost",
          "http://tauri.localhost",
          "https://tauri.localhost",
          "http://localhost",
          "http://127.0.0.1",
        },
      }
    );
    _ipc.RegisterMainThreadCommand("key-limiter.profile.set", SetProfile);
    if (_mirror != null)
    {
      _mirror.Attach(_ipc);
      _ipc.PeerEvent += _mirror.OnPeerEvent;
    }
    _ipc.PeerSubscribed += OnPeerSubscribed;
    _ipc.PeerDisconnected += OnPeerDisconnected;
    _ipc.MarkReady();
    Main.Instance.Log("[IPC] WebSocket namespace ready: " + Namespace);
  }

  public void Dispose()
  {
    _session.Clear();
    if (_ipc == null)
      return;
    _ipc.PeerSubscribed -= OnPeerSubscribed;
    _ipc.PeerDisconnected -= OnPeerDisconnected;
    if (_mirror != null)
      _ipc.PeerEvent -= _mirror.OnPeerEvent;
    AdofaiIpc.AdofaiIpc.UnregisterNamespace(Namespace);
    _ipc = null;
  }

  private void SetProfile(IpcCommand command)
  {
    command.CancellationToken.ThrowIfCancellationRequested();
    KeyLimiterSyncRequestDto payload;
    try
    {
      payload = command.Payload?.ToObject<KeyLimiterSyncRequestDto>();
    }
    catch (Exception)
    {
      command.Reject("invalid_profile", "The key profile could not be read. Select the profile again in ImplDmNote.");
      return;
    }
    KeyLimiterUpdate update =
      payload == null
        ? null
        : new KeyLimiterUpdate
        {
          SchemaVersion = payload.SchemaVersion,
          Source = payload.Source,
          ClientVersion = payload.ClientVersion,
          SessionId = payload.SessionId,
          Revision = payload.Revision,
          Enabled = payload.Enabled,
          Profile =
            payload.Profile == null
              ? null
              : new KeyLimiterProfile { Id = payload.Profile.Id, Name = payload.Profile.Name },
          Keys = payload.Keys,
          ExcludedGhostKeys = payload.ExcludedGhostKeys,
        };
    KeyLimiterApplyResult applied = _session.Apply(command.PeerId, update, command.CancellationToken);
    command.Reply(
      "key-limiter.profile.applied",
      new KeyLimiterSyncResponseDto
      {
        Ok = applied.Ok,
        Applied = applied.Applied,
        Reason = applied.Reason,
        Message = Explain(applied.Reason),
        Enabled = applied.Enabled,
        SessionId = applied.SessionId,
        Revision = applied.Revision,
        SupportedKeys = applied.SupportedKeys,
        UnsupportedKeys = applied.UnsupportedKeys,
      }
    );
    if (applied.Applied)
      _ipc.Publish("key-limiter.state.changed", State());
  }

  private static string Explain(string reason) =>
    reason switch
    {
      "applied" or "duplicate" => null,
      "profile_owned_by_another_app" =>
        "Another ImplDmNote instance controls the key profile. Close that instance and reconnect this app.",
      "unsupported_schema" => "The app and mod use different key-profile versions. Update both and restart ADOFAI.",
      "stale_revision" => "The game already has a newer key profile. Select the current profile again in ImplDmNote.",
      _ => "The game could not apply this key profile. Select the profile again in ImplDmNote.",
    };

  private object State()
  {
    KeyLimiterSnapshot value = _session.Current;
    return new KeyLimiterStatusResponseDto
    {
      SchemaVersion = 2,
      Enabled = value.Enabled,
      Profile = new KeyLimiterProfileDto { Id = value.ProfileId, Name = value.ProfileName },
      SessionId = value.SessionId,
      Revision = value.Revision,
      SupportedKeys = value.SupportedKeys,
      UnsupportedKeys = value.UnsupportedKeys,
    };
  }

  private void OnPeerSubscribed(IpcPeerInfo peer) =>
    _ipc?.SendToPeer(peer.PeerId, "key-limiter.state.changed", State());

  private void OnPeerDisconnected(IpcPeerInfo peer)
  {
    _mirror?.OnPeerDisconnected(peer);
    if (_session.PeerDisconnected(peer.PeerId))
      _ipc?.Publish("key-limiter.state.changed", State());
  }
}
