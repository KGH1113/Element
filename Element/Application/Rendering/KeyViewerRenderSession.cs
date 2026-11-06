using System;
using System.Collections.Generic;
using System.Linq;

namespace Element.Application.Rendering;

/// <summary>
/// One render's key viewers in ElementNote: begin, a frame per captured video
/// frame with the keys pressed since the last, then end. Unity main thread;
/// <see cref="Capture"/> blocks until ElementNote has drawn the frame, which an
/// offline render can afford.
/// </summary>
internal sealed class KeyViewerRenderSession
{
  public const string BeginCommand = "render.begin";
  public const string FrameCommand = "render.frame";
  public const string EndCommand = "render.end";
  public const string PulseCommand = "render.pulse";

  // ElementNote's page clock only moves forward and spends 100 ms starting up,
  // so every time Element sends starts one second after the page's start.
  internal const long TimeOffsetUs = 1_000_000;

  // Keys between two captured frames; a stalled capture must not grow without bound.
  private const int MaxPendingKeys = 4096;
  private static readonly TimeSpan PulseInterval = TimeSpan.FromSeconds(1);

  private readonly IKeyViewerRenderChannel _channel;
  private readonly RenderReplies _replies;
  private readonly List<KeyEvent> _keys = new();
  private string _sessionId;
  private string _beginRequest;
  private RenderSurface[] _surfaces;
  private long _frameIndex;
  private long _lastTimeUs;
  private int _sequence;
  private DateTime _nextPulse;

  public KeyViewerRenderSession(IKeyViewerRenderChannel channel, RenderReplies replies)
  {
    _channel = channel ?? throw new ArgumentNullException(nameof(channel));
    _replies = replies ?? throw new ArgumentNullException(nameof(replies));
  }

  /// <summary>Why the key viewers left the render, or null while they are fine.</summary>
  public RenderFailure Failure { get; private set; }

  /// <summary>A session is open and has not failed.</summary>
  public bool Running => _sessionId != null;

  /// <summary>The pages as ElementNote prepared them; null until its first frame.</summary>
  public IReadOnlyList<RenderSurface> Surfaces => _surfaces;

  /// <summary>Asks ElementNote to prepare the pages. False when no ElementNote takes renders.</summary>
  /// <param name="recordedCounts">
  /// The key counts the rendered run was recorded at (tab id → key slot label → presses), or
  /// null to continue from the player's current totals.
  /// </param>
  public bool Start(
    IReadOnlyList<RenderSurfaceRequest> surfaces,
    IReadOnlyDictionary<string, Dictionary<string, int>> recordedCounts = null
  )
  {
    Stop();
    Failure = null;
    if (surfaces == null || surfaces.Count == 0)
      return false;
    _sessionId = Guid.NewGuid().ToString("N");
    _beginRequest = _channel.Send(
      BeginCommand,
      new
      {
        sessionId = _sessionId,
        surfaces = surfaces
          .Select(surface => new
          {
            viewerKind = surface.ViewerKind,
            scale = surface.Scale,
            surfaceWidth = surface.ViewportWidth,
            surfaceHeight = surface.ViewportHeight,
          })
          .ToArray(),
        frameFormat = "rgba",
        initialOutputTimeUs = 0,
        // The video counts on from the run's recorded key counts, or else from the
        // player's current totals, never from zero.
        counterBaseline = recordedCounts == null
          ? (object)"live"
          : new { mode = "recorded", counters = recordedCounts },
      }
    );
    if (_beginRequest == null)
    {
      Fail(RenderFailure.AppUnavailable, "No ElementNote that draws key viewers for renders is connected.");
      return false;
    }
    return true;
  }

  /// <summary>A recorded key at <paramref name="ticks"/> (100 ns units of video time).</summary>
  public void Input(string key, bool down, long ticks)
  {
    if (!Running || string.IsNullOrEmpty(key) || _keys.Count >= MaxPendingKeys)
      return;
    _keys.Add(new KeyEvent(key, down, _sequence++, ToTimeUs(ticks)));
  }

  /// <summary>
  /// Draws the frame at <paramref name="ticks"/> with the keys pressed since the
  /// last one. Null when the session failed, leaving the reason in <see cref="Failure"/>.
  /// </summary>
  public IReadOnlyList<RenderFrame> Capture(long ticks, TimeSpan beginTimeout, TimeSpan frameTimeout)
  {
    if (!Running)
      return null;
    if (_surfaces == null && !AwaitBegin(beginTimeout))
      return null;

    long timeUs = Math.Max(_lastTimeUs, ToTimeUs(ticks));
    long eventTimeUs = _lastTimeUs;
    var events = new object[_keys.Count];
    for (int i = 0; i < _keys.Count; i++)
    {
      KeyEvent key = _keys[i];
      // Recorded keys never precede the previous frame or follow this one.
      eventTimeUs = Math.Min(timeUs, Math.Max(eventTimeUs, key.TimeUs));
      events[i] = new
      {
        key = key.Key,
        down = key.Down,
        sequence = key.Sequence,
        outputTimeUs = eventTimeUs,
        replayTimeUs = eventTimeUs,
      };
    }
    _keys.Clear();
    _lastTimeUs = timeUs;
    string request = _channel.Send(
      FrameCommand,
      new
      {
        sessionId = _sessionId,
        frameIndex = _frameIndex,
        outputTimeUs = timeUs,
        replayTimeUs = timeUs,
        events,
      }
    );
    if (request == null)
    {
      Fail(RenderFailure.AppUnavailable, "ElementNote disconnected during the render.");
      return null;
    }
    RenderReply reply = _replies.Wait(request, frameTimeout);
    if (!Accept(reply, "draw a frame"))
      return null;
    RenderFrame[] frames = reply.Frames;
    if (
      frames == null
      || frames.Length != _surfaces.Length
      || frames.Any(frame =>
        frame?.FramePath == null || _surfaces.All(surface => surface.ViewerKind != frame.ViewerKind)
      )
    )
    {
      Fail(RenderFailure.Incompatible, "ElementNote answered a frame without the expected viewers.");
      return null;
    }
    _frameIndex++;
    return frames;
  }

  /// <summary>Keeps ElementNote's lease while the render runs between frames.</summary>
  public void Pulse(DateTime now)
  {
    if (!Running || now < _nextPulse)
      return;
    _nextPulse = now + PulseInterval;
    _channel.Send(PulseCommand, new { sessionId = _sessionId });
  }

  /// <summary>The key viewers stop with the rest of the frame from here on.</summary>
  public void Fail(string code, string message)
  {
    Failure ??= new RenderFailure(code, message, _frameIndex > 0);
    Stop();
  }

  /// <summary>Ends the session in ElementNote, which gives live input back.</summary>
  public void Stop()
  {
    if (_sessionId != null)
      _channel.Send(EndCommand, new { sessionId = _sessionId });
    _sessionId = null;
    _beginRequest = null;
    _surfaces = null;
    _frameIndex = 0;
    _lastTimeUs = 0;
    _sequence = 0;
    _keys.Clear();
    _replies.Clear();
  }

  private bool AwaitBegin(TimeSpan timeout)
  {
    RenderReply reply = _replies.Wait(_beginRequest, timeout);
    if (!Accept(reply, "prepare the key viewers"))
      return false;
    RenderSurface[] surfaces = reply.Surfaces;
    if (surfaces == null || surfaces.Length == 0 || surfaces.Any(surface => surface == null || !surface.IsValid()))
    {
      Fail(RenderFailure.Incompatible, "ElementNote prepared key viewers this Element cannot place.");
      return false;
    }
    _surfaces = surfaces;
    return true;
  }

  private bool Accept(RenderReply reply, string action)
  {
    if (reply == null)
    {
      Fail(RenderFailure.Timeout, "ElementNote did not " + action + " in time.");
      return false;
    }
    if (!reply.Ok)
    {
      Fail(reply.ErrorCode, reply.ErrorMessage);
      return false;
    }
    return true;
  }

  private static long ToTimeUs(long ticks) => TimeOffsetUs + Math.Max(0, ticks / 10);

  private readonly struct KeyEvent
  {
    public KeyEvent(string key, bool down, int sequence, long timeUs)
    {
      Key = key;
      Down = down;
      Sequence = sequence;
      TimeUs = timeUs;
    }

    public string Key { get; }
    public bool Down { get; }
    public int Sequence { get; }
    public long TimeUs { get; }
  }
}
