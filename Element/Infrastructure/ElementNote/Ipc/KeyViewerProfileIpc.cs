using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AdofaiIpc.Contracts;
using Element.Application.Profiles;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Element.Infrastructure.ElementNote.Ipc;

/// <summary>
/// Profiles' key viewers on the `element` channel. ElementNote says whose key viewers it
/// shows when it connects, and saves or swaps every key viewer at once when asked. The key
/// viewers themselves travel as files, named by their full paths: with images inside they
/// can be larger than one IPC message.
/// </summary>
/// <remarks>
/// Events arrive on the Unity main thread, so <see cref="Changed"/> fires there and
/// <see cref="Owner"/> changes there; timeouts complete requests on a worker.
/// </remarks>
internal sealed class KeyViewerProfileIpc : IElementIpcEndpoint, IElementNoteKeyViewers
{
  public const string AvailableEvent = "key-viewers.available";
  public const string SaveCommand = "key-viewers.save";
  public const string SaveResultEvent = "key-viewers.save.result";
  public const string SwapCommand = "key-viewers.swap";
  public const string SwapResultEvent = "key-viewers.swap.result";
  private const int ProtocolVersion = 1;

  // Saving reads every image and sound into one file; a large set takes ElementNote a while.
  private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(60);

  private readonly object _gate = new();
  private readonly Dictionary<string, Request> _requests = new(StringComparer.Ordinal);
  private IFeatureRegistration _ipc;
  private string _peerId;
  private string _owner;

  private sealed class Request
  {
    public TaskCompletionSource<bool> Done;
  }

  public event Action Changed;

  public bool Connected
  {
    get
    {
      lock (_gate)
        return _ipc != null && _peerId != null;
    }
  }

  public string Owner
  {
    get
    {
      lock (_gate)
        return _peerId == null ? null : _owner;
    }
  }

  public void AddCommands(IFeatureBuilder builder) { }

  public void OnPeerAttached(IClientConnection peer) { }

  public void Attach(IFeatureRegistration ipc)
  {
    bool left;
    lock (_gate)
    {
      _ipc = ipc;
      left = ipc == null && _peerId != null;
      if (ipc == null)
        _peerId = null;
    }
    if (ipc == null)
      FailRequests();
    if (left)
      Changed?.Invoke();
  }

  public void OnPeerEvent(PeerEvent message)
  {
    switch (message?.Name)
    {
      case AvailableEvent:
        OnAvailable(message);
        return;
      case SaveResultEvent:
      case SwapResultEvent:
        OnResult(message);
        return;
    }
  }

  public void OnPeerDetached(IClientConnection peer)
  {
    lock (_gate)
    {
      if (peer?.PeerId != _peerId)
        return;
      _peerId = null;
      _owner = null;
    }
    FailRequests();
    Changed?.Invoke();
  }

  public Task<bool> SaveAsync(string path) => Send(SaveCommand, new { path });

  public Task<bool> SwapAsync(string savePath, KeyViewersSource load, string owner, string expectedOwner) =>
    Send(
      SwapCommand,
      new
      {
        save = savePath,
        load = load.Kind switch
        {
          KeyViewersSourceKind.File => (object)new { kind = "file", path = load.Path },
          KeyViewersSourceKind.FirstStart => new { kind = "firstStart" },
          _ => new { kind = "keep" },
        },
        owner,
        expected = expectedOwner,
      }
    );

  private void OnAvailable(PeerEvent message)
  {
    JObject payload = Read(message.Json);
    if (payload?.Value<int?>("protocolVersion") != ProtocolVersion)
      return;
    lock (_gate)
    {
      // One ElementNote at a time; another connecting takes over.
      _peerId = message.PeerId;
      _owner = payload["owner"]?.Type == JTokenType.String ? payload.Value<string>("owner") : null;
    }
    Changed?.Invoke();
  }

  // Every answer says whose key viewers ElementNote shows, even one that came after its
  // request timed out: the next swap is planned on it.
  private void OnResult(PeerEvent message)
  {
    JObject payload = Read(message.Json);
    bool ok = payload?.Value<bool?>("ok") == true;
    Request request = null;
    bool ownerChanged = false;
    lock (_gate)
    {
      if (message.PeerId != _peerId)
        return;
      if (message.CorrelationId != null && _requests.TryGetValue(message.CorrelationId, out request))
        _requests.Remove(message.CorrelationId);
      if (payload != null && payload.TryGetValue("owner", out JToken owner))
      {
        string said = owner.Type == JTokenType.String ? owner.Value<string>() : null;
        ownerChanged = said != _owner;
        _owner = said;
      }
    }
    request?.Done.TrySetResult(ok);
    if (ownerChanged)
      Changed?.Invoke();
  }

  private Task<bool> Send(string name, object payload)
  {
    var request = new Request
    {
      Done = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously),
    };
    string requestId;
    lock (_gate)
    {
      if (_ipc == null || _peerId == null)
        return Task.FromResult(false);
      try
      {
        requestId = _ipc.SendCommandToPeer(_peerId, name, JsonConvert.SerializeObject(payload));
      }
      catch (Exception)
      {
        // The app left between the check and the send.
        return Task.FromResult(false);
      }
      if (requestId == null)
        return Task.FromResult(false);
      _requests[requestId] = request;
    }
    Task.Delay(Timeout)
      .ContinueWith(
        _ =>
        {
          lock (_gate)
          {
            // ElementNote may still finish it: whose key viewers it shows is unknown until it
            // says. The next swap expects nobody's, so ElementNote refuses it and says whose.
            if (_requests.Remove(requestId))
              _owner = null;
          }
          request.Done.TrySetResult(false);
        },
        TaskScheduler.Default
      );
    return request.Done.Task;
  }

  private void FailRequests()
  {
    Request[] pending;
    lock (_gate)
    {
      pending = new Request[_requests.Count];
      _requests.Values.CopyTo(pending, 0);
      _requests.Clear();
    }
    foreach (Request request in pending)
      request.Done.TrySetResult(false);
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
