using System;
using System.Collections.Generic;
using UnityEngine;

namespace ImplResourcePack.Domain.Input;

internal sealed class KeyLimiterSnapshot
{
  public static readonly KeyLimiterSnapshot Disabled = new(
    false,
    null,
    null,
    null,
    0,
    Array.Empty<string>(),
    Array.Empty<string>(),
    new HashSet<KeyCode>(),
    new HashSet<ushort>()
  );

  private readonly HashSet<KeyCode> _allowedKeys;
  private readonly HashSet<ushort> _allowedAsyncKeys;

  public bool Enabled { get; }
  public string ProfileId { get; }
  public string ProfileName { get; }
  public string SessionId { get; }
  public long Revision { get; }
  public string[] SupportedKeys { get; }
  public string[] UnsupportedKeys { get; }

  public KeyLimiterSnapshot(
    bool enabled,
    string profileId,
    string profileName,
    string sessionId,
    long revision,
    string[] supportedKeys,
    string[] unsupportedKeys,
    HashSet<KeyCode> allowedKeys,
    HashSet<ushort> allowedAsyncKeys
  )
  {
    Enabled = enabled;
    ProfileId = profileId;
    ProfileName = profileName;
    SessionId = sessionId;
    Revision = revision;
    SupportedKeys = supportedKeys ?? Array.Empty<string>();
    UnsupportedKeys = unsupportedKeys ?? Array.Empty<string>();
    _allowedKeys = allowedKeys ?? new HashSet<KeyCode>();
    _allowedAsyncKeys = allowedAsyncKeys ?? new HashSet<ushort>();
  }

  /// <summary>Unity keys the game accepts while this profile is enabled.</summary>
  public IEnumerable<KeyCode> AllowedKeys => _allowedKeys;

  /// <summary>SkyHook native key codes the game accepts while this profile is enabled.</summary>
  public IEnumerable<ushort> AllowedAsyncKeys => _allowedAsyncKeys;

  public bool Allows(KeyCode key) => _allowedKeys.Contains(key);

  public bool AllowsAsync(ushort key) => _allowedAsyncKeys.Contains(key);
}
