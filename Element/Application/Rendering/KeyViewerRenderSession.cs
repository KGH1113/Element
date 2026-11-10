using System;
using System.Collections.Generic;
using System.Linq;

namespace Element.Application.Rendering;

/// <summary>
/// One render's key viewers in ElementNote: begin, a frame per captured video frame
/// with the keys pressed since the last, finish, then end. ElementNote draws the
/// frames on its own into a video per page, so the game sends them and moves on;
/// only the first frame waits for begin, and finish waits for the videos.
/// Unity main thread, except <see cref="Pulse"/>.
/// </summary>
internal sealed class KeyViewerRenderSession
{
  public const string BeginCommand = "render.begin";
  public const string FrameCommand = "render.frame";
  public const string FinishCommand = "render.finish";
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

  // The lease timer reads the session id and its next pulse.
  private readonly object _pulseGate = new();
  private string _sessionId;
  private DateTime _nextPulse;

  private string _beginRequest;
  private RenderSurface[] _surfaces;
  private int _fps;
  private long _lastFrameIndex = -1;
  private long _lastTimeUs;
  private int _sequence;

  public KeyViewerRenderSession(IKeyViewerRenderChannel channel, RenderReplies replies)
  {
    _channel = channel ?? throw new ArgumentNullException(nameof(channel));
    _replies = replies ?? throw new ArgumentNullException(nameof(replies));
  }

  /// <summary>Why the key viewers left the render, or null while they are fine.</summary>
  public RenderFailure Failure { get; private set; }

  /// <summary>A session is open and has not failed.</summary>
  public bool Running
  {
    get
    {
      lock (_pulseGate)
        return _sessionId != null;
    }
  }

  /// <summary>The pages as ElementNote prepared them; null until its first frame.</summary>
  public IReadOnlyList<RenderSurface> Surfaces => _surfaces;

  /// <summary>Frames sent so far, counting the output frames the video spans.</summary>
  public long FramesSpanned => _lastFrameIndex + 1;

  /// <summary>Asks ElementNote to prepare the pages. False when no ElementNote takes renders.</summary>
  /// <param name="fps">The video's frames per second; ElementNote's videos match it.</param>
  /// <param name="ffmpegPath">The FFmpeg ElementNote encodes the videos with.</param>
  /// <param name="recordedCounts">
  /// The key counts the rendered run was recorded at (tab id → key slot label → presses), or
  /// null to continue from the player's current totals.
  /// </param>
  public bool Start(
    IReadOnlyList<RenderSurfaceRequest> surfaces,
    int fps,
    string ffmpegPath,
    IReadOnlyDictionary<string, Dictionary<string, int>> recordedCounts = null
  )
  {
    Stop();
    Failure = null;
    if (surfaces == null || surfaces.Count == 0)
      return false;
    if (fps <= 0)
      throw new ArgumentOutOfRangeException(nameof(fps));
    if (string.IsNullOrEmpty(ffmpegPath))
      throw new ArgumentException("FFmpeg's path is required.", nameof(ffmpegPath));
    string sessionId = Guid.NewGuid().ToString("N");
    _fps = fps;
    lock (_pulseGate)
    {
      _sessionId = sessionId;
      _nextPulse = DateTime.MinValue;
    }
    _beginRequest = _channel.Send(
      BeginCommand,
      new
      {
        sessionId,
        surfaces = surfaces
          .Select(surface => new
          {
            viewerKind = surface.ViewerKind,
            scale = surface.Scale,
            surfaceWidth = surface.ViewportWidth,
            surfaceHeight = surface.ViewportHeight,
          })
          .ToArray(),
        fps,
        ffmpegPath,
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
  /// Sends the video frame at <paramref name="ticks"/> with the keys pressed since the last
  /// one, without waiting for ElementNote to draw it. The first frame waits for begin. False
  /// when the session failed, leaving the reason in <see cref="Failure"/>.
  /// </summary>
  public bool SendFrame(long ticks, TimeSpan beginTimeout)
  {
    if (!Running)
      return false;
    if (_surfaces == null && !AwaitBegin(beginTimeout))
      return false;

    long frameIndex = FrameIndex(ticks, _fps);
    // Still the same output frame: its keys go with the next one.
    if (frameIndex <= _lastFrameIndex)
      return true;
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
    string request = _channel.Send(
      FrameCommand,
      new
      {
        sessionId = SessionId,
        frameIndex,
        outputTimeUs = timeUs,
        replayTimeUs = timeUs,
        events,
      }
    );
    if (request == null)
    {
      Fail(RenderFailure.AppUnavailable, "ElementNote disconnected during the render.");
      return false;
    }
    _keys.Clear();
    _lastTimeUs = timeUs;
    _lastFrameIndex = frameIndex;
    return true;
  }

  /// <summary>
  /// Asks ElementNote to finish drawing every frame sent and waits for the videos, one per
  /// page. Null when no frame was sent, or when the session failed (see <see cref="Failure"/>).
  /// </summary>
  public IReadOnlyList<RenderVideo> Finish(TimeSpan timeout)
  {
    if (!Running || _surfaces == null || _lastFrameIndex < 0)
      return null;
    string request = _channel.Send(FinishCommand, new { sessionId = SessionId });
    if (request == null)
    {
      Fail(RenderFailure.AppUnavailable, "ElementNote disconnected before the key viewer videos were done.");
      return null;
    }
    RenderReply reply = _replies.Wait(request, timeout);
    if (!Accept(reply, "finish the key viewer videos"))
      return null;
    RenderVideo[] videos = reply.Videos;
    if (
      videos == null
      || videos.Length != _surfaces.Length
      || videos.Any(video => video == null || !video.IsValid())
      || _surfaces.Any(surface => videos.Count(video => video.ViewerKind == surface.ViewerKind) != 1)
    )
    {
      Fail(RenderFailure.Incompatible, "ElementNote finished without a video for each key viewer.");
      return null;
    }
    return videos;
  }

  /// <summary>Keeps ElementNote's lease while the render runs. Any thread.</summary>
  public void Pulse(DateTime now)
  {
    string sessionId;
    lock (_pulseGate)
    {
      if (_sessionId == null || now < _nextPulse)
        return;
      _nextPulse = now + PulseInterval;
      sessionId = _sessionId;
    }
    _channel.Send(PulseCommand, new { sessionId });
  }

  /// <summary>The key viewers leave the video.</summary>
  public void Fail(string code, string message)
  {
    Failure ??= new RenderFailure(code, message);
    Stop();
  }

  /// <summary>Ends the session in ElementNote, which deletes its videos and gives live input back.</summary>
  public void Stop()
  {
    string sessionId;
    lock (_pulseGate)
    {
      sessionId = _sessionId;
      _sessionId = null;
    }
    if (sessionId != null)
      _channel.Send(EndCommand, new { sessionId });
    _beginRequest = null;
    _surfaces = null;
    _fps = 0;
    _lastFrameIndex = -1;
    _lastTimeUs = 0;
    _sequence = 0;
    _keys.Clear();
    _replies.Clear();
  }

  /// <summary>
  /// How long finish may take for a video of <paramref name="frames"/> frames. ElementNote
  /// draws alongside the game and may lag far behind a fast render, so this allows two
  /// frames a second plus two minutes. ElementNote times out its own stuck pages, and a
  /// disconnect ends the wait at once.
  /// </summary>
  public static TimeSpan FinishTimeout(long frames) =>
    TimeSpan.FromSeconds(Math.Min(3 * 60 * 60, 120 + Math.Max(0, frames) * .5));

  /// <summary>The output frame at <paramref name="ticks"/> of video time, at <paramref name="fps"/>.</summary>
  internal static long FrameIndex(long ticks, int fps) =>
    (long)Math.Round(Math.Max(0, ticks) * (double)fps / TimeSpan.TicksPerSecond, MidpointRounding.AwayFromZero);

  private string SessionId
  {
    get
    {
      lock (_pulseGate)
        return _sessionId;
    }
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
      // A disconnect clears the replies; the session is gone either way.
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
