using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AdofaiIpc.Contracts;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using KeyCounts = System.Collections.Generic.Dictionary<string, System.Collections.Generic.Dictionary<string, int>>;

namespace Element.Infrastructure.Ipc;

/// <summary>
/// ElementNote's key counts on the `element` channel. Element reads them as a run starts
/// recording, and tells ElementNote while TUFReplay replays a run in the game: the replay's
/// presses then count from the run's recorded counts and stay out of the player's totals.
/// </summary>
/// <remarks>
/// Counts are ElementNote tab id → key slot label → presses. Events arrive on the Unity main
/// thread; reads complete there too, and their timeouts on a worker.
/// </remarks>
internal sealed class KeyCounterIpc
{
  public const string AvailableEvent = "counters.available";
  public const string ReadCommand = "counters.read";
  public const string ReadResultEvent = "counters.read.result";
  public const string ReplayBeginCommand = "counters.replay.begin";
  public const string ReplayEndCommand = "counters.replay.end";
  private const int ProtocolVersion = 1;

  private readonly object _gate = new();
  private readonly Dictionary<string, TaskCompletionSource<KeyCounts>> _reads = new(StringComparer.Ordinal);
  private IFeatureRegistration _ipc;
  private string _peerId;
  private bool _replaying;
  private KeyCounts _replayCounts;

  /// <summary>An ElementNote that shares its key counts is connected.</summary>
  public bool Available
  {
    get
    {
      lock (_gate)
        return _ipc != null && _peerId != null;
    }
  }

  public void Attach(IFeatureRegistration ipc)
  {
    lock (_gate)
    {
      _ipc = ipc;
      if (ipc == null)
        _peerId = null;
    }
    if (ipc == null)
      FailReads();
  }

  public void OnPeerEvent(PeerEvent message)
  {
    if (message?.Name == AvailableEvent)
    {
      OnAvailable(message);
      return;
    }
    if (message?.Name != ReadResultEvent || message.CorrelationId == null)
      return;
    TaskCompletionSource<KeyCounts> read;
    lock (_gate)
    {
      if (message.PeerId != _peerId || !_reads.TryGetValue(message.CorrelationId, out read))
        return;
      _reads.Remove(message.CorrelationId);
    }
    read.TrySetResult(ParseCounts(message.Json));
  }

  public void OnPeerDisconnected(string peerId)
  {
    lock (_gate)
    {
      if (peerId != _peerId)
        return;
      _peerId = null;
    }
    FailReads();
  }

  /// <summary>ElementNote's counts now, or null when no ElementNote answers within <paramref name="timeout"/>.</summary>
  public Task<KeyCounts> ReadAsync(TimeSpan timeout)
  {
    var read = new TaskCompletionSource<KeyCounts>(TaskCreationOptions.RunContinuationsAsynchronously);
    string requestId;
    lock (_gate)
    {
      requestId = Send(ReadCommand, new { });
      if (requestId == null)
        return Task.FromResult<KeyCounts>(null);
      _reads[requestId] = read;
    }
    Task.Delay(timeout)
      .ContinueWith(
        _ =>
        {
          lock (_gate)
            _reads.Remove(requestId);
          read.TrySetResult(null);
        },
        TaskScheduler.Default
      );
    return read.Task;
  }

  /// <summary>
  /// TUFReplay starts replaying a run in the game. <paramref name="recorded"/> holds the
  /// counts it was recorded at, or null for a run recorded without them.
  /// </summary>
  public void BeginReplay(KeyCounts recorded)
  {
    lock (_gate)
    {
      _replaying = true;
      _replayCounts = recorded;
      Send(ReplayBeginCommand, new { counters = recorded });
    }
  }

  public void EndReplay()
  {
    lock (_gate)
    {
      if (!_replaying)
        return;
      _replaying = false;
      _replayCounts = null;
      Send(ReplayEndCommand, new { });
    }
  }

  private void OnAvailable(PeerEvent message)
  {
    if (Read(message.Json)?.Value<int?>("protocolVersion") != ProtocolVersion)
      return;
    lock (_gate)
    {
      _peerId ??= message.PeerId;
      // An ElementNote that connects during a replay counts the rest of it the same way.
      if (_replaying && message.PeerId == _peerId)
        Send(ReplayBeginCommand, new { counters = _replayCounts });
    }
  }

  // Call while holding _gate.
  private string Send(string name, object payload)
  {
    if (_ipc == null || _peerId == null)
      return null;
    try
    {
      return _ipc.SendCommandToPeer(_peerId, name, JsonConvert.SerializeObject(payload));
    }
    catch (Exception)
    {
      // The app left between the check and the send.
      return null;
    }
  }

  private void FailReads()
  {
    TaskCompletionSource<KeyCounts>[] pending;
    lock (_gate)
    {
      pending = new TaskCompletionSource<KeyCounts>[_reads.Count];
      _reads.Values.CopyTo(pending, 0);
      _reads.Clear();
    }
    foreach (TaskCompletionSource<KeyCounts> read in pending)
      read.TrySetResult(null);
  }

  /// <summary>The counts in a read result; null when it failed or can't be read.</summary>
  internal static KeyCounts ParseCounts(string json)
  {
    JObject root = Read(json);
    if (root?.Value<bool?>("ok") != true || root["counters"] is not JObject tabs)
      return null;
    var counts = new KeyCounts(StringComparer.Ordinal);
    foreach (JProperty tab in tabs.Properties())
    {
      if (tab.Value is not JObject keys)
        return null;
      var tabCounts = new Dictionary<string, int>(StringComparer.Ordinal);
      foreach (JProperty key in keys.Properties())
      {
        // Counts are whole presses; anything else means the reply is not ElementNote's.
        if (key.Value.Type != JTokenType.Integer)
          return null;
        long presses = key.Value.Value<long>();
        if (presses < 0 || presses > int.MaxValue)
          return null;
        tabCounts[key.Name] = (int)presses;
      }
      counts[tab.Name] = tabCounts;
    }
    return counts;
  }

  private static JObject Read(string json)
  {
    try
    {
      return string.IsNullOrEmpty(json) ? null : JObject.Parse(json);
    }
    catch (JsonException)
    {
      return null;
    }
  }
}
