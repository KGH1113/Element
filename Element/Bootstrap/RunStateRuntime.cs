using System;
using System.Collections.Generic;
using Element.Infrastructure.Replays;

namespace Element.Bootstrap;

/// <summary>
/// Element's state in TUFReplay's runs: the attempt counts and ElementNote key counts each
/// run was played at, shown again when TUFReplay replays or renders that run. Registers with
/// TUFReplay once it loads, and again after it is turned off and on. Unity main thread.
/// </summary>
internal sealed class RunStateRuntime : IDisposable
{
  private static readonly TimeSpan RegisterInterval = TimeSpan.FromSeconds(2);

  private readonly TufReplayRunStateApi _api = new();
  private readonly KeyValuePair<string, object>[] _providers;
  private readonly Action<string> _log;
  private DateTime _nextRegisterCheck;
  private bool _announced;
  private bool _disposed;

  public RunStateRuntime(AttemptRunStateProvider attempts, KeyCountRunStateProvider keyCounts, Action<string> log)
  {
    _providers = new[]
    {
      new KeyValuePair<string, object>(AttemptRunStateProvider.Id, attempts),
      new KeyValuePair<string, object>(KeyCountRunStateProvider.Id, keyCounts),
    };
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
      _log("[TUFReplay] Element's attempts and key counts are saved with TUFReplay's runs.");
    }
  }

  public void Dispose()
  {
    if (_disposed)
      return;
    _disposed = true;
    foreach (KeyValuePair<string, object> provider in _providers)
      _api.Unregister(provider.Key);
    _api.Dispose();
  }
}
