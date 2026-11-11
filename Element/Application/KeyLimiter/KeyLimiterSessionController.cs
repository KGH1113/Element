using System.Threading;
using Element.Domain.Input;

namespace Element.Application.KeyLimiter;

/** Application policy owns the profile and disconnect lifecycle, independently of sockets. */
internal sealed class KeyLimiterSessionController
{
  private readonly IKeyLimiterProfileApplier _profiles;
  private readonly object _gate = new();
  private string _owner;

  public KeyLimiterSessionController(IKeyLimiterProfileApplier profiles) => _profiles = profiles;

  public KeyLimiterSnapshot Current => _profiles.Current;

  public KeyLimiterApplyResult Apply(string peer, KeyLimiterUpdate update, CancellationToken cancellation = default)
  {
    lock (_gate)
    {
      // Disconnect clears under the same gate; canceled queued work cannot reclaim ownership.
      cancellation.ThrowIfCancellationRequested();
      if (string.IsNullOrWhiteSpace(peer))
        return KeyLimiterApplyResult.Invalid("invalid_peer");
      string reason = Validate(update);
      if (reason != null)
        return KeyLimiterApplyResult.Invalid(reason);
      if (_owner != null && _owner != peer)
        return KeyLimiterApplyResult.Invalid("profile_owned_by_another_app");
      KeyLimiterApplyResult result = _profiles.Apply(update);
      if (result.Ok)
        _owner = peer;
      return result;
    }
  }

  public bool PeerDisconnected(string peer)
  {
    lock (_gate)
    {
      if (_owner != peer)
        return false;
      _owner = null;
      _profiles.Clear();
      return true;
    }
  }

  public void Clear()
  {
    lock (_gate)
    {
      _owner = null;
      _profiles.Clear();
    }
  }

  private static string Validate(KeyLimiterUpdate update)
  {
    if (update == null)
      return "invalid_profile";
    if (update.SchemaVersion != 2)
      return "unsupported_schema";
    if (update.Source != "element-note")
      return "invalid_source";
    if (string.IsNullOrWhiteSpace(update.ClientVersion))
      return "invalid_client_version";
    if (string.IsNullOrWhiteSpace(update.SessionId))
      return "invalid_session";
    if (update.Revision <= 0)
      return "invalid_revision";
    if (update.Profile == null || string.IsNullOrWhiteSpace(update.Profile.Id))
      return "invalid_profile";
    if (update.Keys == null || update.Keys.Length > 512 || update.Keys.AnyTooLong())
      return "invalid_keys";
    return null;
  }
}
