using System;
using System.Threading.Tasks;
using AdofaiIpc.Contracts;
using Element.Application.KeyLimiter;
using Element.Domain.KeyLimiter;
using Newtonsoft.Json;

namespace Element.Infrastructure.ElementNote.Ipc;

/// <summary>ElementNote's key profile, applied through the game's key limiter, and its state sent back.</summary>
internal sealed class KeyLimiterIpc : IElementIpcEndpoint
{
  private const string ProfileCommand = "key-limiter.profile.set";
  private readonly KeyLimiterSessionController _session;
  private IFeatureRegistration _ipc;

  public KeyLimiterIpc(KeyLimiterSessionController session) => _session = session;

  public void AddCommands(IFeatureBuilder builder) =>
    builder.OnCommand(
      ProfileCommand,
      IpcExecutionContext.GameThread,
      command =>
      {
        SetProfile(command);
        return Task.CompletedTask;
      }
    );

  /// <summary>The channel closing drops the profile, so the game gets the player's own keys back.</summary>
  public void Attach(IFeatureRegistration ipc)
  {
    if (ipc == null)
      _session.Clear();
    _ipc = ipc;
  }

  public void OnPeerAttached(IClientConnection peer) =>
    _ipc?.SendToPeer(peer.PeerId, "key-limiter.state.changed", JsonConvert.SerializeObject(State()));

  public void OnPeerEvent(PeerEvent message) { }

  public void OnPeerDetached(IClientConnection peer)
  {
    if (_session.PeerDisconnected(peer.PeerId))
      _ipc?.Publish("key-limiter.state.changed", JsonConvert.SerializeObject(State()));
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
      command.Reject("invalid_profile", "The key profile could not be read. Select the profile again in ElementNote.");
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
        "Another ElementNote instance controls the key profile. Close that instance and reconnect this app.",
      "unsupported_schema" => "The app and mod use different key-profile versions. Update both and restart ADOFAI.",
      "stale_revision" => "The game already has a newer key profile. Select the current profile again in ElementNote.",
      _ => "The game could not apply this key profile. Select the profile again in ElementNote.",
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
}
