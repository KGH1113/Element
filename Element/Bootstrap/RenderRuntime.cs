using System;
using System.Collections.Generic;
using System.Linq;
using Element.Application;
using Element.Application.Rendering;
using Element.Domain.DmNote;
using Element.Infrastructure.Rendering;
using Element.Presentation.Rendering;
using Element.Presentation.Settings;
using UnityEngine;

namespace Element.Bootstrap;

/// <summary>
/// Element in the videos TUFReplay renders. The overlay is a canvas the renderer captures.
/// ElementNote draws the key viewers from the run's recorded keys into videos alongside the
/// render, never holding a frame up, and the renderer lays those videos over the finished
/// video. Plays in any offline render stay out of the attempt count. Normal renders find
/// the overlay on their own and show no key viewers.
/// </summary>
internal sealed class RenderRuntime : IRenderOverlay, IDisposable
{
  private const string SkipRuleOwner = "OfflineRender";

  // ElementNote opens its pages and loads fonts before the first frame.
  private static readonly TimeSpan BeginTimeout = TimeSpan.FromSeconds(30);

  // Pulses go out about once a second; the session spaces them.
  private static readonly TimeSpan LeaseTick = TimeSpan.FromMilliseconds(500);

  private readonly IReplayRenderTarget _target;
  private readonly IKeyViewerRenderApp _app;
  private readonly KeyViewerRenderSession _session;
  private readonly Func<Canvas> _overlayCanvas;
  private readonly Func<int, int, IReadOnlyList<RenderViewerPlacement>> _placements;
  private readonly Func<bool> _appConnected;
  private readonly Action<string, string> _notify;
  private readonly Action<string> _logWarning;
  private readonly RecordedKeyCounts _recordedCounts;
  private readonly List<Canvas> _canvases = new(1);
  private IReadOnlyList<RenderViewerPlacement> _viewers;
  private RenderFailure _startFailure;
  private System.Threading.Timer _lease;
  private bool _pending;
  private long _ticks;
  private bool _disposed;

  /// <param name="target">TUFReplay's API, for the video's size and rate; its owner disposes it.</param>
  /// <param name="video">Whether a video is being rendered offline now.</param>
  /// <param name="placements">The key viewers to show in a video of the given size.</param>
  /// <param name="appConnected">ElementNote is connected for the in-game viewers.</param>
  /// <param name="recordedCounts">The key counts of the run TUFReplay renders, if it saved them.</param>
  public RenderRuntime(
    IReplayRenderTarget target,
    IRenderVideo video,
    IKeyViewerRenderApp app,
    RecordedKeyCounts recordedCounts,
    Func<Canvas> overlayCanvas,
    Func<int, int, IReadOnlyList<RenderViewerPlacement>> placements,
    Func<bool> appConnected,
    Action<string, string> notify,
    Action<string> logWarning
  )
  {
    _target = target ?? throw new ArgumentNullException(nameof(target));
    if (video == null)
      throw new ArgumentNullException(nameof(video));
    _app = app ?? throw new ArgumentNullException(nameof(app));
    _overlayCanvas = overlayCanvas;
    _placements = placements;
    _appConnected = appConnected;
    _notify = notify;
    _logWarning = logWarning ?? (_ => { });
    _recordedCounts = recordedCounts;
    _session = new KeyViewerRenderSession(app, app.Replies);
    Provider = new ReplayRenderProvider(this);
    // The renderer plays the level itself; those plays are not attempts.
    AttemptCounter.Rules.Set(SkipRuleOwner, () => video.IsRendering);
  }

  /// <summary>The object registered with TUFReplay under <see cref="ReplayRenderProvider.Id"/>.</summary>
  public ReplayRenderProvider Provider { get; }

  public IEnumerable<Canvas> Canvases()
  {
    StartIfPending();
    _canvases.Clear();
    Canvas overlay = _overlayCanvas?.Invoke();
    if (overlay != null)
      _canvases.Add(overlay);
    return _canvases;
  }

  // TUFReplay begins its providers one after another, so the key viewers start once the
  // video is used: by then the run's recorded counts are in and the video has its size.
  public void Begin()
  {
    StopKeyViewers();
    _startFailure = null;
    _ticks = 0;
    _pending = true;
  }

  public void Clock(long ticks)
  {
    _ticks = ticks;
    StartIfPending();
  }

  public void Input(string key, bool down, long ticks) => _session.Input(key, down, ticks);

  // Sends the frame's keys and moves on; ElementNote draws it in its own time.
  public void BeforeCapture()
  {
    StartIfPending();
    if (_session.Running)
      _session.SendFrame(_ticks, BeginTimeout);
  }

  // After the last frame of a completed render. Waits until ElementNote has drawn every frame.
  public void AddVideos(Action<string, RectInt, RectInt> add)
  {
    if (_viewers == null || !_session.Running)
      return;
    IReadOnlyList<RenderVideo> videos = _session.Finish(KeyViewerRenderSession.FinishTimeout(_session.FramesSpanned));
    foreach (KeyViewerOverlay overlay in KeyViewerRenderPlan.Overlays(_viewers, _session.Surfaces, videos))
      add(overlay.Path, ToRect(overlay.Source), ToRect(overlay.Target));
  }

  // After the renderer laid the videos over the video: ElementNote may delete them now.
  public void End()
  {
    _pending = false;
    RenderFailure failure = _startFailure ?? _session.Failure;
    _startFailure = null;
    StopKeyViewers();
    if (failure != null)
      Report(failure);
  }

  public void Dispose()
  {
    if (_disposed)
      return;
    _disposed = true;
    _pending = false;
    StopKeyViewers();
    AttemptCounter.Rules.Remove(SkipRuleOwner);
  }

  private void StartIfPending()
  {
    if (!_pending)
      return;
    _pending = false;
    int width = _target.RenderWidth;
    int height = _target.RenderHeight;
    IReadOnlyList<RenderViewerPlacement> placements =
      width > 0 && height > 0 ? _placements(width, height) : Array.Empty<RenderViewerPlacement>();
    if (placements.Count == 0)
      return;
    _startFailure = KeyViewerRenderPlan.Blocker(_target, _app.Available, _appConnected?.Invoke() == true);
    if (_startFailure != null)
      return;
    // Several viewers of a kind share one page, drawn at the largest of their sizes.
    RenderSurfaceRequest[] surfaces = placements
      .GroupBy(placement => placement.ViewerKind)
      .Select(kind => new RenderSurfaceRequest(
        kind.Key,
        kind.Max(placement => placement.Scale),
        kind.First().ViewportWidth,
        kind.First().ViewportHeight
      ))
      .ToArray();
    // An ElementNote that can't take recorded counts continues from the player's totals.
    Dictionary<string, Dictionary<string, int>> recorded = _app.TakesRecordedCounts ? _recordedCounts?.Counts : null;
    if (!_session.Start(surfaces, _target.RenderFps, _target.RenderFfmpegPath, recorded))
      return;
    _viewers = placements;
    // The game thread blocks while finish waits; the lease must not run out meanwhile.
    _lease = new System.Threading.Timer(_ => Pulse(), null, TimeSpan.Zero, LeaseTick);
  }

  private void Pulse()
  {
    try
    {
      _session.Pulse(DateTime.UtcNow);
    }
    catch (Exception)
    {
      // A timer thread must never throw; a missed pulse is caught up by the next one.
    }
  }

  private void StopKeyViewers()
  {
    _lease?.Dispose();
    _lease = null;
    _session.Stop();
    _viewers = null;
  }

  // After the render: the notice must not appear in the video.
  private void Report(RenderFailure failure)
  {
    _logWarning("[Render] Key viewers left the render (" + failure.Code + "): " + failure.Message);
    (string title, string detail) = RenderNotices.For(failure.Code, SettingsLocale.Korean);
    _notify?.Invoke(title, detail);
  }

  private static RectInt ToRect(PixelRect rect) => new(rect.X, rect.Y, rect.Width, rect.Height);
}
