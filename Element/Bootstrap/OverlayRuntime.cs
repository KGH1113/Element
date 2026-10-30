using System;
using Element.Application;
using Element.Application.Features;
using Element.Infrastructure.Assets;
using Element.Infrastructure.Game.Bpm;
using Element.Infrastructure.Game.Judgement;
using Element.Infrastructure.Game.Status;
using Element.Infrastructure.Game.TimingScale;
using Element.Presentation.Overlay.Attempt;
using Element.Presentation.Overlay.Bpm;
using Element.Presentation.Overlay.Combo;
using Element.Presentation.Overlay.Judgement;
using Element.Presentation.Overlay.Shared;
using Element.Presentation.Overlay.Status;
using Element.Presentation.Overlay.TimingScale;
using TMPro;

namespace Element.Bootstrap;

/// <summary>
/// Owns the in-game overlay: its canvas, font, views and the status clock.
/// The canvas exists even without the font so the layout editor still works.
/// </summary>
internal sealed class OverlayRuntime : IDisposable
{
  private FontBundleLoader _fontLoader;
  private OverlayMaterialFactory _materials;
  private OverlayCoordinator _coordinator;
  private StatusTimeTicker _timeTicker;

  /// <param name="attempts">Null when attempt counting could not start.</param>
  public OverlayRuntime(Main main, Action<int> onTimeTick, IAttemptSource attempts)
  {
    try
    {
      Canvas = new OverlayCanvasHost();
      Canvas.ConfigureLayouts(main.Settings.OverlayLayouts);
      _fontLoader = new FontBundleLoader(main);
      if (!_fontLoader.Load())
      {
        _fontLoader.Dispose();
        _fontLoader = null;
        return;
      }

      TMP_FontAsset font = _fontLoader.FontAsset;
      _materials = new OverlayMaterialFactory(font);
      _coordinator = new OverlayCoordinator(
        new JudgementFeature(new JudgementReader(), new JudgementView(Canvas, font, _materials.Material)),
        new StatusFeature(new StatusReader(), new StatusView(Canvas, font, _materials.Material)),
        new BpmFeature(new BpmReader(), new BpmView(Canvas, font, _materials.Material)),
        new ComboFeature(new ComboView(Canvas, font, _materials.Material)),
        new TimingScaleFeature(new TimingScaleReader(), new TimingScaleView(Canvas, font, _materials.Material)),
        new AttemptFeature(attempts, new AttemptView(Canvas, font, _materials.Material))
      );
      _timeTicker = Canvas.Root.AddComponent<StatusTimeTicker>();
      _timeTicker.Initialize(onTimeTick);
      // Views only repaint when their data changes; repaint them once per color change.
      OverlayTheme.Changed += RefreshTheme;
    }
    catch
    {
      Dispose();
      throw;
    }
  }

  public OverlayCanvasHost Canvas { get; private set; }
  public bool Available => _coordinator != null;
  public TMP_FontAsset Font => _fontLoader?.FontAsset;

  public void Show()
  {
    Canvas?.SetVisible(true);
    _coordinator?.Show();
    _timeTicker?.StartTicking();
  }

  public void Hide()
  {
    _timeTicker?.StopTicking();
    _coordinator?.Hide();
    Canvas?.SetVisible(false);
  }

  public void RegisterHit(scrMarginTracker tracker, HitMargin hit) => _coordinator?.RegisterHit(tracker, hit);

  public void RefreshAfterReset(scrMarginTracker tracker)
  {
    _coordinator?.RefreshAfterReset(tracker);
    if (_coordinator is { IsVisible: true })
      _timeTicker?.StartTicking();
  }

  public void RefreshAfterDeath(scrMarginTracker tracker)
  {
    _timeTicker?.StopTicking();
    _coordinator?.RefreshAfterReset(tracker);
  }

  public void RefreshAccuracy() => _coordinator?.RefreshAccuracy();

  public void RefreshFloor() => _coordinator?.RefreshFloor();

  public void RefreshTime(int framesPerSecond) => _coordinator?.RefreshTime(framesPerSecond);

  /// <summary>Frees CJK glyphs pulled in for song titles. Only while the overlay is hidden.</summary>
  public void ReleaseFallbackGlyphs() => _fontLoader?.ReleaseFallbackGlyphs();

  private void RefreshTheme() => _coordinator?.RefreshAll();

  public void Dispose()
  {
    OverlayTheme.Changed -= RefreshTheme;
    Hide();
    _coordinator?.Dispose();
    _coordinator = null;
    _timeTicker = null;
    Canvas?.Dispose();
    Canvas = null;
    _materials?.Dispose();
    _materials = null;
    _fontLoader?.Dispose();
    _fontLoader = null;
  }
}
