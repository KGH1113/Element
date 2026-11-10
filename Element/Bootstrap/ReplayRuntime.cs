using System;
using System.Collections.Generic;
using Element.Infrastructure.Replays;

namespace Element.Bootstrap;

/// <summary>
/// Element in TUFReplay's runs: the attempt counts and ElementNote key counts each run was
/// played at, shown again when TUFReplay replays or renders that run, and the overlay and key
/// viewers in the videos it renders. Registers with TUFReplay once it loads, and again after it
/// is turned off and on. Unity main thread.
/// </summary>
internal sealed class ReplayRuntime : IDisposable
{
  private static readonly TimeSpan RegisterInterval = TimeSpan.FromSeconds(2);

  private readonly TufReplayApi _api;
  private readonly KeyValuePair<string, object>[] _providers;
  private readonly Action<string> _log;
  private DateTime _nextRegisterCheck;
  private bool _announced;
  private bool _disposed;

  /// <param name="api">TUFReplay's API; its owner disposes it.</param>
  /// <param name="providers">Each provider by the id TUFReplay keeps it under.</param>
  public ReplayRuntime(TufReplayApi api, KeyValuePair<string, object>[] providers, Action<string> log)
  {
    _api = api ?? throw new ArgumentNullException(nameof(api));
    _providers = providers ?? throw new ArgumentNullException(nameof(providers));
    _log = log ?? (_ => { });
  }

  /// <summary>Unity's per-frame update; cheap until TUFReplay loads and between checks.</summary>
  public void Tick()
  {
    if (_disposed)
      return;
    DateTime now = DateTime.UtcNow;
    if (now < _nextRegisterCheck)
      return;
    _nextRegisterCheck = now + RegisterInterval;
    if (!_api.TryResolve())
      return;
    bool registered = true;
    foreach (KeyValuePair<string, object> provider in _providers)
      if (!_api.IsRegistered(provider.Key))
        registered &= _api.Register(provider.Key, provider.Value);
    if (registered && !_announced)
    {
      _announced = true;
      _log("[TUFReplay] Element follows TUFReplay's replays and renders.");
    }
  }

  public void Dispose()
  {
    if (_disposed)
      return;
    _disposed = true;
    if (!_api.Resolved)
      return;
    foreach (KeyValuePair<string, object> provider in _providers)
      _api.Unregister(provider.Key);
  }
}
