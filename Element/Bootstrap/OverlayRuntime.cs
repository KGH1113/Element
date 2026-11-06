using System;
using System.IO;
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
using Element.Presentation.Overlay.Elements;
using Element.Presentation.Overlay.Judgement;
using Element.Presentation.Overlay.Shared;
using Element.Presentation.Overlay.Status;
using Element.Presentation.Overlay.TimingScale;
using TMPro;

namespace Element.Bootstrap;

/// <summary>
/// Owns the in-game overlay: its canvas, font, the features that publish values
/// and the status clock. The canvas exists even without the font so the layout
/// editor still works.
/// </summary>
internal sealed class OverlayRuntime : IDisposable
{
  private FontBundleLoader _fontLoader;
  private OverlayMaterialFactory _materials;
  private OverlayFonts _fonts;
  private OverlayCoordinator _coordinator;
  private StatusTimeTicker _timeTicker;

  /// <param name="attempts">Null when attempt counting could not start.</param>
  /// <param name="video">OrbitRender's video, which values follow while it renders.</param>
  public OverlayRuntime(Main main, Action<int> onTimeTick, IAttemptSource attempts, IRenderVideo video)
  {
    try
    {
      Canvas = new OverlayCanvasHost();
      _fontLoader = new FontBundleLoader(main);
      if (!_fontLoader.Load())
      {
        _fontLoader.Dispose();
        _fontLoader = null;
        Canvas.ConfigureLayouts(main.Settings.OverlayLayouts);
        return;
      }

      TMP_FontAsset font = _fontLoader.FontAsset;
      _materials = new OverlayMaterialFactory(font);
      // Chosen fonts live in the mod root, so updates keep them.
      OverlayFontCatalog catalog = new(
        Path.Combine(main.Path, FontsFolder),
        FontFilePickers.ForPlatform(),
        main.LogWarning,
        main.LogException
      );
      _fonts = new OverlayFonts(font, _materials.Material, catalog, catalog);
      _fonts.SetDefault(main.Settings.OverlayFont);
      Canvas.SetFonts(_fonts);
      Canvas.ConfigureLayouts(main.Settings.OverlayLayouts);
      OverlayValues values = Canvas.Values;
      _coordinator = new OverlayCoordinator(
        new JudgementFeature(new JudgementReader(), new JudgementValues(values)),
        new StatusFeature(new StatusReader(video), new StatusValues(values)),
        new BpmFeature(new BpmReader(), new BpmValues(values)),
        new ComboFeature(new ComboValues(Canvas, video)),
        new TimingScaleFeature(new TimingScaleReader(), new TimingScaleValues(values)),
        new AttemptFeature(attempts, new AttemptValues(values))
      );
      _timeTicker = Canvas.Root.AddComponent<StatusTimeTicker>();
      _timeTicker.Initialize(onTimeTick, video);
    }
    catch
    {
      Dispose();
      throw;
    }
  }

  public const string FontsFolder = "Fonts";

  public OverlayCanvasHost Canvas { get; private set; }
  public bool Available => _coordinator != null;

  /// <summary>The bundled font, which the settings UI also uses.</summary>
  public TMP_FontAsset Font => _fontLoader?.FontAsset;

  /// <summary>The fonts overlay texts draw with; null without the bundled font.</summary>
  public OverlayFonts Fonts => _fonts;

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

  /// <summary>Redraws every value while the overlay is shown; nothing while it is hidden.</summary>
  public void RefreshAll() => _coordinator?.RefreshAll();

  public void RefreshFloor() => _coordinator?.RefreshFloor();

  public void RefreshTime(int framesPerSecond) => _coordinator?.RefreshTime(framesPerSecond);

  /// <summary>Frees glyphs pulled in for song titles. Only while the overlay is hidden.</summary>
  public void ReleaseFallbackGlyphs()
  {
    _fontLoader?.ReleaseFallbackGlyphs();
    _fonts?.ReleaseGlyphs();
  }

  public void Dispose()
  {
    Hide();
    _coordinator?.Dispose();
    _coordinator = null;
    _timeTicker = null;
    Canvas?.Dispose();
    Canvas = null;
    _fonts?.Dispose();
    _fonts = null;
    _materials?.Dispose();
    _materials = null;
    _fontLoader?.Dispose();
    _fontLoader = null;
  }
}
