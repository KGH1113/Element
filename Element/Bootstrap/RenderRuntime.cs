using System;
using System.Collections.Generic;
using System.Linq;
using Element.Application;
using Element.Application.Rendering;
using Element.Domain.DmNote;
using Element.Infrastructure.Ipc;
using Element.Infrastructure.Rendering;
using Element.Infrastructure.Replays;
using Element.Presentation.Rendering;
using Element.Presentation.Settings;
using UnityEngine;

namespace Element.Bootstrap;

/// <summary>
/// Element in the videos TUFReplay renders: the overlay, and ElementNote's key viewers drawn
/// frame by frame from the run's recorded keys. Plays in any offline render stay out of the
/// attempt count. Normal renders find the overlay on their own and show no key viewers.
/// </summary>
internal sealed class RenderRuntime : IRenderOverlay, IDisposable
{
  private const string SkipRuleOwner = "OfflineRender";

  // ElementNote opens its pages and loads fonts before the first frame.
  private static readonly TimeSpan BeginTimeout = TimeSpan.FromSeconds(30);
  private static readonly TimeSpan FrameTimeout = TimeSpan.FromSeconds(15);

  private readonly TufReplayApi _api;
  private readonly KeyViewerRenderIpc _renders;
  private readonly KeyViewerRenderSession _session;
  private readonly Func<Canvas> _overlayCanvas;
  private readonly Func<int, int, IReadOnlyList<RenderViewerPlacement>> _placements;
  private readonly Func<bool> _appConnected;
  private readonly Action<string, string> _notify;
  private readonly Action<string> _logWarning;
  private readonly RecordedKeyCounts _recordedCounts;
  private readonly List<Canvas> _canvases = new(2);
  private RenderKeyViewerCanvas _keyViewers;
  private RenderFailure _startFailure;
  private bool _pending;
  private bool _configured;
  private long _ticks;
  private bool _disposed;

  /// <param name="api">TUFReplay's API, for the video's size; its owner disposes it.</param>
  /// <param name="video">Whether a video is being rendered offline now.</param>
  /// <param name="placements">The key viewers to show in a video of the given size.</param>
  /// <param name="appConnected">ElementNote is connected for the in-game viewers.</param>
  /// <param name="recordedCounts">The key counts of the run TUFReplay renders, if it saved them.</param>
  public RenderRuntime(
    TufReplayApi api,
    IRenderVideo video,
    KeyViewerRenderIpc renders,
    RecordedKeyCounts recordedCounts,
    Func<Canvas> overlayCanvas,
    Func<int, int, IReadOnlyList<RenderViewerPlacement>> placements,
    Func<bool> appConnected,
    Action<string, string> notify,
    Action<string> logWarning
  )
  {
    _api = api ?? throw new ArgumentNullException(nameof(api));
    if (video == null)
      throw new ArgumentNullException(nameof(video));
    _renders = renders ?? throw new ArgumentNullException(nameof(renders));
    _overlayCanvas = overlayCanvas;
    _placements = placements;
    _appConnected = appConnected;
    _notify = notify;
    _logWarning = logWarning ?? (_ => { });
    _recordedCounts = recordedCounts;
    _session = new KeyViewerRenderSession(renders, renders.Replies);
    Provider = new ReplayRenderProvider(this);
    // The renderer plays the level itself; those plays are not attempts.
    AttemptCounter.Rules.Set(SkipRuleOwner, () => video.IsRendering);
  }

  /// <summary>The object registered with TUFReplay under <see cref="ReplayRenderProvider.Id"/>.</summary>
  public ReplayRenderProvider Provider { get; }

  /// <summary>Unity's per-frame update: keeps ElementNote's lease.</summary>
  public void Tick() => _session.Pulse(DateTime.UtcNow);

  public IEnumerable<Canvas> Canvases()
  {
    StartIfPending();
    _canvases.Clear();
    Canvas overlay = _overlayCanvas?.Invoke();
    if (overlay != null)
      _canvases.Add(overlay);
    if (_keyViewers != null)
      _canvases.Add(_keyViewers.Canvas);
    return _canvases;
  }

  // TUFReplay begins its providers one after another, so the key viewers start once the
  // video is used: by then the run's recorded counts are in and the video has its size.
  public void Begin()
  {
    EndKeyViewers();
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

  // Blocks until ElementNote has drawn this frame; the render waits, the video does not change.
  public void BeforeCapture()
  {
    StartIfPending();
    if (_keyViewers == null)
      return;
    if (!_session.Running)
    {
      _keyViewers.Hide();
      return;
    }
    IReadOnlyList<RenderFrame> frames = _session.Capture(_ticks, BeginTimeout, FrameTimeout);
    if (frames == null)
    {
      _keyViewers.Hide();
      return;
    }
    if (!_configured)
    {
      _keyViewers.Configure(_session.Surfaces);
      _configured = true;
    }
    if (!_keyViewers.Show(frames, out string error))
    {
      _session.Fail(RenderFailure.FrameUnreadable, error);
      _keyViewers.Hide();
    }
  }

  public void End()
  {
    _pending = false;
    RenderFailure failure = _startFailure ?? _session.Failure;
    _startFailure = null;
    _session.Stop();
    EndKeyViewers();
    if (failure != null)
      Report(failure);
  }

  public void Dispose()
  {
    if (_disposed)
      return;
    _disposed = true;
    _pending = false;
    _session.Stop();
    EndKeyViewers();
    AttemptCounter.Rules.Remove(SkipRuleOwner);
  }

  private const string AppOutdated = "app-outdated";

  private void StartIfPending()
  {
    if (!_pending)
      return;
    _pending = false;
    int width = _api.RenderWidth;
    int height = _api.RenderHeight;
    IReadOnlyList<RenderViewerPlacement> placements =
      width > 0 && height > 0 ? _placements(width, height) : Array.Empty<RenderViewerPlacement>();
    if (placements.Count == 0)
      return;
    if (!_renders.Available)
    {
      _startFailure = new RenderFailure(
        _appConnected?.Invoke() == true ? AppOutdated : RenderFailure.AppUnavailable,
        "ElementNote does not draw key viewers for renders.",
        false
      );
      return;
    }
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
    Dictionary<string, Dictionary<string, int>> recorded = _renders.TakesRecordedCounts
      ? _recordedCounts?.Counts
      : null;
    if (!_session.Start(surfaces, recorded))
      return;
    _keyViewers = new RenderKeyViewerCanvas(placements);
    _configured = false;
  }

  private void EndKeyViewers()
  {
    _keyViewers?.Dispose();
    _keyViewers = null;
    _configured = false;
  }

  // After the render: the notice must not appear in the video.
  private void Report(RenderFailure failure)
  {
    _logWarning("[Render] Key viewers left the render (" + failure.Code + "): " + failure.Message);
    string title = failure.AfterFirstFrame
      ? SettingsLocale.L("영상 중간부터 키뷰어가 빠졌어요", "The key viewers stop partway through the video")
      : SettingsLocale.L("키뷰어 없이 렌더링했어요", "Rendered without the key viewers");
    _notify?.Invoke(title, Explain(failure.Code));
  }

  private static string Explain(string code) =>
    code switch
    {
      RenderFailure.AppUnavailable => SettingsLocale.L(
        "ElementNote를 켠 뒤 다시 렌더링하면 키뷰어가 들어가요.",
        "Open ElementNote, then render again to include them."
      ),
      AppOutdated or RenderFailure.Incompatible => SettingsLocale.L(
        "ElementNote를 최신 버전으로 업데이트한 뒤 다시 렌더링해주세요.",
        "Update ElementNote, then render again."
      ),
      "render-busy" => SettingsLocale.L(
        "다른 렌더가 ElementNote를 쓰고 있었어요. 그 렌더가 끝난 뒤 다시 렌더링해주세요.",
        "Another render was using ElementNote. Render again after it finishes."
      ),
      "render-platform-unsupported" => SettingsLocale.L(
        "이 운영체제에서는 아직 키뷰어를 영상에 넣을 수 없어요.",
        "This system can't put key viewers into videos yet."
      ),
      _ => SettingsLocale.L(
        "ElementNote가 응답하지 않았어요. ElementNote를 다시 연 뒤 렌더링해주세요.",
        "ElementNote stopped answering. Reopen ElementNote, then render again."
      ),
    };
}
