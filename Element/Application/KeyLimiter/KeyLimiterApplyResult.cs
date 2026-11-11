using System;
using Element.Domain.Input;

namespace Element.Application.KeyLimiter;

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
