using System;
using System.Collections.Generic;
using System.Linq;
using Element.Application.Rendering;
using Element.Domain.DmNote;
using Element.Infrastructure.Ipc;
using Element.Infrastructure.Rendering;
using Element.Presentation.Rendering;
using Element.Presentation.Settings;
using UnityEngine;

namespace Element.Bootstrap;

/// <summary>
/// Element in OrbitRender's renders: the overlay in every render, and in replay
/// renders ElementNote's key viewers, drawn frame by frame from the recorded
/// keys. Plays OrbitRender renders stay out of the attempt count.
/// </summary>
internal sealed class RenderRuntime : IRenderOverlay, IDisposable
{
  private const string ProviderId = "Element";
  private const string SkipRuleOwner = "OrbitRender";
  private static readonly TimeSpan RegisterInterval = TimeSpan.FromSeconds(2);

  // ElementNote opens its pages and loads fonts before the first frame.
  private static readonly TimeSpan BeginTimeout = TimeSpan.FromSeconds(30);
  private static readonly TimeSpan FrameTimeout = TimeSpan.FromSeconds(15);

  private readonly OrbitRenderApi _api;
  private readonly OrbitRenderProvider _provider;
  private readonly KeyViewerRenderIpc _renders;
  private readonly KeyViewerRenderSession _session;
  private readonly Func<Canvas> _overlayCanvas;
  private readonly Func<int, int, IReadOnlyList<RenderViewerPlacement>> _placements;
  private readonly Func<bool> _appConnected;
  private readonly Action<string, string> _notify;
  private readonly Action<string> _log;
  private readonly Action<string> _logWarning;
  private readonly RecordedKeyCounts _recordedCounts;
  private RenderKeyViewerCanvas _keyViewers;
  private RenderFailure _startFailure;
  private bool _configured;
  private long _ticks;
  private DateTime _nextRegisterCheck;
  private bool _disposed;

  /// <param name="api">OrbitRender's API; the overlay reads it too, so its owner disposes it.</param>
  /// <param name="placements">The key viewers to show in a video of the given size.</param>
  /// <param name="appConnected">ElementNote is connected for the in-game viewers.</param>
  /// <param name="recordedCounts">The key counts of the run TUFReplay renders, if it saved them.</param>
  public RenderRuntime(
    OrbitRenderApi api,
    KeyViewerRenderIpc renders,
    RecordedKeyCounts recordedCounts,
    Func<Canvas> overlayCanvas,
    Func<int, int, IReadOnlyList<RenderViewerPlacement>> placements,
    Func<bool> appConnected,
    Action<string, string> notify,
    Action<string> log,
    Action<string> logWarning
  )
  {
    _api = api ?? throw new ArgumentNullException(nameof(api));
    _renders = renders ?? throw new ArgumentNullException(nameof(renders));
    _overlayCanvas = overlayCanvas;
    _placements = placements;
    _appConnected = appConnected;
    _notify = notify;
    _log = log ?? (_ => { });
    _logWarning = logWarning ?? (_ => { });
    _recordedCounts = recordedCounts;
    _session = new KeyViewerRenderSession(renders, renders.Replies);
    _provider = new OrbitRenderProvider(this);
    // OrbitRender plays the recorded level itself; those plays are not attempts.
    AttemptCounter.Rules.Set(SkipRuleOwner, () => _api.IsRendering);
  }

  /// <summary>Unity's per-frame update: joins OrbitRender once it loads and keeps ElementNote's lease.</summary>
  public void Tick()
  {
    DateTime now = DateTime.UtcNow;
    _session.Pulse(now);
    if (now < _nextRegisterCheck)
      return;
    _nextRegisterCheck = now + RegisterInterval;
    // Registering again covers OrbitRender being turned off and on in UnityModManager.
    if (_api.TryResolve() && !_api.IsRegistered(ProviderId) && _api.Register(ProviderId, _provider))
      _log("[OrbitRender] Element's overlay and key viewers join OrbitRender renders.");
  }

  public IEnumerable<Canvas> Canvases()
  {
    Canvas overlay = _overlayCanvas?.Invoke();
    if (overlay != null)
      yield return overlay;
    if (_keyViewers != null)
      yield return _keyViewers.Canvas;
  }

  public void Begin(bool replay)
  {
    EndKeyViewers();
    _startFailure = null;
    _ticks = 0;
    // A normal render has no recorded keys, so the key viewers stay out of it.
    if (!replay)
      return;
    int width = _api.Width;
    int height = _api.Height;
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

  public void Clock(long ticks) => _ticks = ticks;

  public void Input(string key, bool down, long ticks) => _session.Input(key, down, ticks);

  // Blocks until ElementNote has drawn this frame; the render waits, the video does not change.
  public void BeforeCapture()
  {
    if (_keyViewers == null)
      return;
    if (!_session.Running)
    {
      _keyViewers.Hide();
      return;
    }
    IReadOnlyList<RenderFrame> frames = _session.Capture(_api.VideoTicks ?? _ticks, BeginTimeout, FrameTimeout);
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
    _session.Stop();
    EndKeyViewers();
    if (_api.Resolved)
      _api.Unregister(ProviderId);
    AttemptCounter.Rules.Remove(SkipRuleOwner);
  }

  private const string AppOutdated = "app-outdated";

  private void EndKeyViewers()
  {
    _keyViewers?.Dispose();
    _keyViewers = null;
    _configured = false;
  }

  // After the render: the notice must not appear in the video.
  private void Report(RenderFailure failure)
  {
    _logWarning("[OrbitRender] Key viewers left the render (" + failure.Code + "): " + failure.Message);
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
