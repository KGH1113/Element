using System.Collections.Generic;
using UnityEngine;

namespace Element.Application.KeyLimiter;

internal sealed class KeyLimiterKeySelection
{
  public HashSet<KeyCode> AllowedKeys = new();
  public HashSet<ushort> AllowedAsyncKeys = new();
  public string[] Supported = System.Array.Empty<string>();
  public string[] Unsupported = System.Array.Empty<string>();
}

internal interface IKeyLimiterKeyResolver
{
  KeyLimiterKeySelection Resolve(string[] requestedKeys, bool enabled);
}
