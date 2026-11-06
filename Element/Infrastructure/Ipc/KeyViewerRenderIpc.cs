using System;
using System.Linq;
using AdofaiIpc.Contracts;
using Element.Application.Rendering;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Element.Infrastructure.Ipc;

/// <summary>
/// The ElementNote that draws key viewers for renders, on the `element` channel.
/// Its answers come back as <see cref="ReplyCommand"/>, which runs off the game
/// thread: the game thread is the one waiting for them.
/// </summary>
internal sealed class KeyViewerRenderIpc : IKeyViewerRenderChannel
{
  public const string ReplyCommand = "render.reply";
  public const string AvailableEvent = "render.available";
  private const int ProtocolVersion = 1;

  private readonly object _gate = new();
  private IFeatureRegistration _ipc;
  private string _peerId;
  private bool _takesRecordedCounts;

  public RenderReplies Replies { get; } = new();

  /// <summary>
  /// The connected ElementNote can start a render's key counts from a run's recorded counts
  /// (<c>render.available</c> carries <c>recordedCounters: true</c>).
  /// </summary>
  public bool TakesRecordedCounts
  {
    get
    {
      lock (_gate)
        return _peerId != null && _takesRecordedCounts;
    }
  }

  /// <summary>An ElementNote that takes renders is connected.</summary>
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
      _ipc = ipc;
  }

  // Unity main thread: IPC events are dispatched there.
  public void OnPeerEvent(PeerEvent message)
  {
    if (message?.Name != AvailableEvent)
      return;
    JObject available = Read(message.Json);
    if (available?.Value<int?>("protocolVersion") != ProtocolVersion)
      return;
    lock (_gate)
    {
      _peerId ??= message.PeerId;
      if (message.PeerId == _peerId)
        _takesRecordedCounts = available.Value<bool?>("recordedCounters") == true;
    }
  }

  public void OnPeerDisconnected(string peerId)
  {
    lock (_gate)
    {
      if (peerId != _peerId)
        return;
      _peerId = null;
    }
    // A waiting frame fails now instead of at its timeout.
    Replies.Clear();
  }

  // IPC worker thread (Background command).
  public void OnReply(IIncomingMessage message)
  {
    string peer;
    lock (_gate)
      peer = _peerId;
    if (message.Client?.PeerId != peer)
    {
      message.Reject("render_not_owner", "Only the ElementNote that renders key viewers can answer.");
      return;
    }
    RenderReply reply = Parse(message.Json);
    if (reply == null)
    {
      message.Reject("render_reply_invalid", "The render reply could not be read.");
      return;
    }
    Replies.Post(reply);
    message.Reply("render.reply.received", "{}");
  }

  public string Send(string name, object payload)
  {
    lock (_gate)
    {
      if (_ipc == null || _peerId == null)
        return null;
      try
      {
        return _ipc.SendCommandToPeer(_peerId, name, JsonConvert.SerializeObject(payload));
      }
      catch (Exception)
      {
        // The app left between the check and the send; the caller reports it.
        return null;
      }
    }
  }

  /// <summary>Reads ElementNote's reply; null when it is not one.</summary>
  internal static RenderReply Parse(string json)
  {
    JObject root = Read(json);
    string requestId = root?.Value<string>("requestId");
    if (requestId == null || root["ok"]?.Type != JTokenType.Boolean)
      return null;
    var reply = new RenderReply { RequestId = requestId, Ok = root.Value<bool>("ok") };
    if (!reply.Ok)
    {
      JObject error = root["error"] as JObject;
      reply.ErrorCode = error?.Value<string>("code");
      reply.ErrorMessage = error?.Value<string>("message");
      return reply;
    }
    if (root["result"] is not JObject result)
      return reply;
    if (result["surfaces"] is JArray surfaces)
      reply.Surfaces = surfaces.OfType<JObject>().Select(ReadSurface).ToArray();
    if (result["frames"] is JArray frames)
      reply.Frames = frames
        .OfType<JObject>()
        .Select(frame => new RenderFrame
        {
          ViewerKind = frame.Value<string>("viewerKind"),
          FramePath = frame.Value<string>("framePath"),
        })
        .ToArray();
    return reply;
  }

  private static RenderSurface ReadSurface(JObject surface)
  {
    // Without its viewport a page cannot be placed; IsValid rejects it.
    JObject viewport = surface["viewport"] as JObject;
    return new RenderSurface
    {
      ViewerKind = surface.Value<string>("viewerKind"),
      Width = surface.Value<int?>("width") ?? 0,
      Height = surface.Value<int?>("height") ?? 0,
      FrameFormat = surface.Value<string>("frameFormat"),
      ViewportWidth = viewport?.Value<float?>("width") ?? 0,
      ViewportHeight = viewport?.Value<float?>("height") ?? 0,
      Padding = viewport?.Value<float?>("padding") ?? 0,
    };
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
