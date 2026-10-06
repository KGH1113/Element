using System;
using ImplResourcePack.Domain.Input;
using Newtonsoft.Json;

namespace ImplResourcePack.Infrastructure.Ipc;

public sealed class KeyLimiterProfileDto
{
  [JsonProperty("id")]
  public string Id;

  [JsonProperty("name")]
  public string Name;
}

public sealed class KeyLimiterSyncRequestDto
{
  [JsonProperty("schemaVersion")]
  public int SchemaVersion;

  [JsonProperty("source")]
  public string Source;

  [JsonProperty("clientVersion")]
  public string ClientVersion;

  [JsonProperty("sessionId")]
  public string SessionId;

  [JsonProperty("revision")]
  public long Revision;

  [JsonProperty("enabled")]
  public bool Enabled;

  [JsonProperty("profile")]
  public KeyLimiterProfileDto Profile;

  [JsonProperty("keys")]
  public string[] Keys;

  [JsonProperty("excludedGhostKeys")]
  public string[] ExcludedGhostKeys;
}

public sealed class KeyLimiterSyncResponseDto
{
  [JsonProperty("ok")]
  public bool Ok;

  [JsonProperty("applied")]
  public bool Applied;

  [JsonProperty("reason")]
  public string Reason;

  [JsonProperty("message")]
  public string Message;

  [JsonProperty("enabled")]
  public bool Enabled;

  [JsonProperty("sessionId")]
  public string SessionId;

  [JsonProperty("revision")]
  public long Revision;

  [JsonProperty("supportedKeys")]
  public string[] SupportedKeys;

  [JsonProperty("unsupportedKeys")]
  public string[] UnsupportedKeys;

  internal static KeyLimiterSyncResponseDto From(KeyLimiterSnapshot snapshot, bool applied, string reason) =>
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

  public static KeyLimiterSyncResponseDto Invalid(string reason) =>
    new()
    {
      Ok = false,
      Applied = false,
      Reason = reason,
      Enabled = false,
      Revision = 0,
      SupportedKeys = Array.Empty<string>(),
      UnsupportedKeys = Array.Empty<string>(),
    };
}

public sealed class KeyLimiterStatusResponseDto
{
  [JsonProperty("schemaVersion")]
  public int SchemaVersion;

  [JsonProperty("enabled")]
  public bool Enabled;

  [JsonProperty("profile")]
  public KeyLimiterProfileDto Profile;

  [JsonProperty("sessionId")]
  public string SessionId;

  [JsonProperty("revision")]
  public long Revision;

  [JsonProperty("supportedKeys")]
  public string[] SupportedKeys;

  [JsonProperty("unsupportedKeys")]
  public string[] UnsupportedKeys;
}

public sealed class HealthResponseDto
{
  [JsonProperty("ok")]
  public bool Ok;

  [JsonProperty("mod")]
  public string Mod;

  [JsonProperty("modVersion")]
  public string ModVersion;

  [JsonProperty("protocolVersion")]
  public int ProtocolVersion;

  [JsonProperty("serverVersion")]
  public int ServerVersion;
}
