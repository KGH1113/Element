namespace Element.Application.KeyLimiter;

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
