using System;
using System.Collections.Generic;
using Element.Application.KeyLimiter;

namespace Element.Infrastructure.Game.Input;

internal sealed class DmNoteKeyResolver : IKeyLimiterKeyResolver
{
  public KeyLimiterKeySelection Resolve(string[] requestedKeys, bool enabled)
  {
    KeyLimiterKeySelection result = new();
    if (!enabled)
      return result;
    List<string> supported = new();
    List<string> unsupported = new();
    HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);
    foreach (string rawKey in requestedKeys ?? Array.Empty<string>())
    {
      string key = rawKey?.Trim() ?? string.Empty;
      if (string.IsNullOrEmpty(key) || !seen.Add(key))
        continue;
      if (!DmNoteKeyMapper.TryResolve(key, out ResolvedLimiterKey resolved))
      {
        unsupported.Add(key);
        continue;
      }
      supported.Add(key);
      result.AllowedKeys.Add(resolved.UnityKey);
      DmNoteKeyMapper.AddAsyncKeys(resolved, result.AllowedAsyncKeys);
    }
    result.Supported = supported.ToArray();
    result.Unsupported = unsupported.ToArray();
    return result;
  }
}
