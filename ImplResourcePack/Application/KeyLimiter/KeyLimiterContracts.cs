using System;
using System.Threading;
using ImplResourcePack.Domain.Input;

namespace ImplResourcePack.Application.KeyLimiter;

internal sealed class KeyLimiterProfile
{
  public string Id;
  public string Name;
}

internal sealed class KeyLimiterUpdate
{
  public int SchemaVersion;
  public string Source;
  public string ClientVersion;
  public string SessionId;
  public long Revision;
  public bool Enabled;
  public KeyLimiterProfile Profile;
  public string[] Keys;
  public string[] ExcludedGhostKeys;
}

internal sealed class KeyLimiterApplyResult
{
  public bool Ok;
  public bool Applied;
  public string Reason;
  public bool Enabled;
  public string SessionId;
  public long Revision;
  public string[] SupportedKeys;
  public string[] UnsupportedKeys;

  public static KeyLimiterApplyResult From(KeyLimiterSnapshot snapshot, bool applied, string reason) =>
    new()
    {
      Ok = true,
      Applied = applied,
      Reason = reason,
      Enabled = snapshot.Enabled,
      SessionId = snapshot.SessionId,
      Revision = snapshot.Revision,
      SupportedKeys = snapshot.SupportedKeys,
      UnsupportedKeys = snapshot.UnsupportedKeys,
    };

  public static KeyLimiterApplyResult Invalid(string reason) =>
    new()
    {
      Reason = reason,
      SupportedKeys = Array.Empty<string>(),
      UnsupportedKeys = Array.Empty<string>(),
    };
}

internal interface IKeyLimiterProfileApplier
{
  KeyLimiterSnapshot Current { get; }
  KeyLimiterApplyResult Apply(KeyLimiterUpdate update);
  void Clear();
}

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
    if (update.Source != "impl-dm-note")
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

internal static class KeyLimiterKeyBounds
{
  public static bool AnyTooLong(this string[] keys)
  {
    foreach (string key in keys)
      if (key == null || key.Length > 128)
        return true;
    return false;
  }
}
