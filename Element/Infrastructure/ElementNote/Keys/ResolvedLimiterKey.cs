using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using SkyHook;
using UnityEngine;

namespace Element.Infrastructure.ElementNote.Keys;

internal readonly struct ResolvedLimiterKey
{
  public KeyCode UnityKey { get; }
  public bool HasAsyncKey { get; }
  public ushort AsyncKey { get; }

  public ResolvedLimiterKey(KeyCode unityKey, bool hasAsyncKey, ushort asyncKey)
  {
    UnityKey = unityKey;
    HasAsyncKey = hasAsyncKey;
    AsyncKey = asyncKey;
  }
}
