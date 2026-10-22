using System;
using System.IO;
using System.Threading.Tasks;
using AdofaiIpc.Contracts;
using AdofaiIpc.Loader;
using ImplResourcePack.Application.KeyLimiter;
using ImplResourcePack.Domain.Input;
using Newtonsoft.Json;

namespace ImplResourcePack.Infrastructure.Ipc;

internal sealed class KeyLimiterIpcFeature : IDisposable
{
  private const string Namespace = "impl-resourcepack";
  private readonly KeyLimiterSessionController _session;
  private DmNoteNativeIpcFeature _nativeViewers;
  private IFeatureRegistration _ipc;

  public KeyLimiterIpcFeature(KeyLimiterSessionController session) => _session = session;

  public void AttachNativeViewers(DmNoteNativeIpcFeature nativeViewers) => _nativeViewers = nativeViewers;

  public void Enable()
  {
    if (_ipc != null)
      return;
    string mods = Directory
      .GetParent(
        Path.GetFullPath(Main.Instance.Path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
      )
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
        Main.Instance.Version,
        protocolMajor: 1,
        displayName: "ImplResourcePack",
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
        builder.OnCommand(
          "key-limiter.profile.set",
          IpcExecutionContext.GameThread,
          command =>
          {
            SetProfile(command);
            return Task.CompletedTask;
          }
        );
        builder.OnClientAttached(OnPeerSubscribed);
        builder.OnClientDetached(OnPeerDisconnected);
        builder.OnEvent(message =>
        {
          _nativeViewers?.OnPeerEvent(message);
        });
      }
    );
    _nativeViewers?.Attach(_ipc);
    _ipc.MarkReady();
    Main.Instance.Log("[IPC] WebSocket namespace ready: " + Namespace);
  }

  public void Dispose()
  {
    _session.Clear();
    if (_ipc == null)
      return;
    _ipc.Dispose();
    _ipc = null;
  }

  private void SetProfile(IIncomingMessage command)
  {
    command.CancellationToken.ThrowIfCancellationRequested();
    KeyLimiterSyncRequestDto payload;
    try
    {
      payload = JsonConvert.DeserializeObject<KeyLimiterSyncRequestDto>(command.Json);
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
    KeyLimiterApplyResult applied = _session.Apply(command.Client.PeerId, update, command.CancellationToken);
    command.Reply(
      "key-limiter.profile.applied",
      JsonConvert.SerializeObject(
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
      )
    );
    if (applied.Applied)
      _ipc.Publish("key-limiter.state.changed", JsonConvert.SerializeObject(State()));
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

  private void OnPeerSubscribed(IClientConnection peer) =>
    _ipc?.SendToPeer(peer.PeerId, "key-limiter.state.changed", JsonConvert.SerializeObject(State()));

  private void OnPeerDisconnected(IClientConnection peer)
  {
    _nativeViewers?.OnPeerDisconnected(peer);
    if (_session.PeerDisconnected(peer.PeerId))
      _ipc?.Publish("key-limiter.state.changed", JsonConvert.SerializeObject(State()));
  }
}
