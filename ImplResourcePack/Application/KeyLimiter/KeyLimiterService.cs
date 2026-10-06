using System;
using System.Threading;
using ImplResourcePack.Domain.Input;

namespace ImplResourcePack.Application.KeyLimiter;

internal sealed class KeyLimiterService : IKeyLimiterProfileApplier
{
  private readonly object _updateGate = new();
  private readonly IKeyLimiterKeyResolver _keys;

  public KeyLimiterService(IKeyLimiterKeyResolver keys) => _keys = keys;

  private KeyLimiterSnapshot _current = KeyLimiterSnapshot.Disabled;

  public KeyLimiterSnapshot Current => Volatile.Read(ref _current);

  public KeyLimiterApplyResult Apply(KeyLimiterUpdate request)
  {
    lock (_updateGate)
    {
      KeyLimiterSnapshot current = Current;
      if (string.Equals(current.SessionId, request.SessionId, StringComparison.Ordinal))
      {
        if (request.Revision == current.Revision)
          return KeyLimiterApplyResult.From(current, false, "duplicate");
        if (request.Revision < current.Revision)
          return KeyLimiterApplyResult.From(current, false, "stale_revision");
      }

      KeyLimiterKeySelection selected = _keys.Resolve(request.Keys, request.Enabled);
      bool enabled = request.Enabled && selected.Supported.Length > 0;
      KeyLimiterSnapshot next = new(
        enabled,
        request.Profile.Id,
        request.Profile.Name,
        request.SessionId,
        request.Revision,
        selected.Supported,
        selected.Unsupported,
        selected.AllowedKeys,
        selected.AllowedAsyncKeys
      );
      Volatile.Write(ref _current, next);
      return KeyLimiterApplyResult.From(next, true, "applied");
    }
  }

  public void Clear()
  {
    lock (_updateGate)
      Volatile.Write(ref _current, KeyLimiterSnapshot.Disabled);
  }
}
