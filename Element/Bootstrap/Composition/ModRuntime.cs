using System;
using System.Collections.Generic;
using System.Linq;
using Element.Application.Attempts;
using Element.Application.Gameplay;
using Element.Application.Input;
using Element.Application.Judgement;
using Element.Application.KeyLimiter;
using Element.Application.KeyViewer;
using Element.Application.Rendering;
using Element.Bootstrap.Hosting;
using Element.Configuration;
using Element.Configuration.Gameplay;
using Element.Domain.Rendering;
using Element.Infrastructure.ElementNote.App;
using Element.Infrastructure.ElementNote.Ipc;
using Element.Infrastructure.ElementNote.Keys;
using Element.Infrastructure.ElementNote.Viewers;
using Element.Infrastructure.Game.Editor;
using Element.Infrastructure.Game.Effects;
using Element.Infrastructure.Game.Input;
using Element.Infrastructure.Game.Patches;
using Element.Infrastructure.Game.Planets;
using Element.Infrastructure.Game.Play;
using Element.Infrastructure.Game.Recording;
using Element.Infrastructure.Game.Settings;
using Element.Infrastructure.Mods;
using Element.Infrastructure.Storage;
using Element.Infrastructure.TufReplay;
using Element.Presentation.Layout.KeyViewers;
using Element.Presentation.Notices;
using Element.Presentation.Ui.Theme;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Element.Bootstrap.Composition;

/// <summary>
/// Composes the mod: makes its parts, connects them to each other and to the game,
/// and takes them apart again. The overlay and the settings UI are composed in their
/// own runtimes; what the game tells the mod goes through <see cref="GameEvents"/>.
/// </summary>
internal sealed class ModRuntime : IDisposable
{
  private const string HarmonyId = "Element";

  private readonly Main _main;
  private readonly FailureGuard _guard;
  private readonly GameplayState _playState = new();
  private Harmony _harmony;
  private KeyLimiterService _keyLimiter;
  private GameKeyLimiter _gameKeyLimiter;
  private ElementIpcChannel _ipcChannel;
  private ElementNoteNativeViewers _nativeViewers;
  private KeyViewerRenderIpc _renderIpc;
  private KeyCounterIpc _counterIpc;
  private RenderSession _render;
  private ReplayRuntime _replays;
  private ReplayedAttempts _replayedAttempts;
  private readonly RecordedKeyCounts _recordedKeyCounts = new();
  private TufReplayApi _tufReplay;
  private readonly OfflineRender _offlineRender = new();
  private ElementNoteAppLauncher _elementNoteApp;
  private RecordingModeController _recordingMode;
  private EditorTrackpadController _trackpad;
  private GuardedEditorTweaks _editorTweaks;
  private GuardedPlanetAppearance _planetAppearance;
  private EffectsController _effects;
  private AttemptRunReader _attemptRuns;
  private AttemptTracker _attempts;
  private HitTextHideSync _hitTextSync;
  private OverlayRuntime _overlay;
  private SettingsUiRuntime _settingsUi;
  private GameplaySession _session;
  private GameEvents _events;
  private FrameLoop _frameLoop;
  private Action _applySettings;
  private bool _sceneHooked;
  private bool _initialized;
  private bool _disposed;

  internal KeyLimiterService KeyLimiter => _keyLimiter;
  internal IReadOnlyList<string> FailedPatchGroups { get; private set; } = Array.Empty<string>();
  public bool OverlaysAvailable => _overlay?.Available == true;

  public ModRuntime(Main main)
  {
    _main = main;
    _guard = new FailureGuard(main.LogException, main.LogWarning);
  }

  public bool Initialize()
  {
    if (_initialized)
      return true;
    if (_disposed)
      throw new ObjectDisposedException(nameof(ModRuntime));

    try
    {
      InputGate.Connect(GameAsyncInput.Clear);
      _keyLimiter = new KeyLimiterService(new ElementNoteKeyResolver());
      _gameKeyLimiter = CreateGameKeyLimiter(_keyLimiter);
      GameHooks.KeyLimiter = _gameKeyLimiter;

      _harmony = new Harmony(HarmonyId);
      FailedPatchGroups = PatchGroups.Apply(_harmony, typeof(Main).Assembly, _main.LogException);
      if (!FailedPatchGroups.Contains(PatchGroupNames.Trackpad))
        _trackpad = EditorTrackpadController.TryCreate(
          _main.RuntimePath,
          () => _main.Settings.NativeEditorTrackpad,
          _main.Log,
          _main.LogException
        );
      GameHooks.Trackpad = _trackpad;
      GameHooks.Chatter = _main.Chatter;
      _editorTweaks = new GuardedEditorTweaks(_main.Settings.Gameplay.Editor, _main.LogException);
      GameHooks.EditorTweaks = _editorTweaks;
      _planetAppearance = new GuardedPlanetAppearance(_main.Settings.Gameplay.Planets, _main.LogException);
      GameHooks.PlanetAppearance = _planetAppearance;
      _effects = new EffectsController(() => _main.Settings.Effects);
      GameHooks.Effects = _effects;
      _recordingMode = new RecordingModeController();
      var gameHitText = new GameHitTextHide(
        () => _events?.OnGameHitTextHideChanged(),
        () => _events?.OnCompetitiveModeChanged()
      );
      GameHooks.HitTextHide = gameHitText;
      _hitTextSync = new HitTextHideSync(
        () => _main.Settings.Gameplay.Judgement,
        gameHitText,
        () => _settingsUi?.Save()
      );
      SceneManager.sceneUnloaded += OnSceneUnloaded;
      _sceneHooked = true;
      KeyboardChatterBlockerCheck.WarnIfLoaded(_main.LogWarning);

      _attempts = CreateAttemptTracker();
      // During TUFReplay's replays and renders the overlay shows the run's recorded attempts.
      _replayedAttempts = new ReplayedAttempts(_attempts);
      _tufReplay = new TufReplayApi();
      _overlay = new OverlayRuntime(
        _main,
        framesPerSecond => _events?.OnTimeTick(framesPerSecond),
        _replayedAttempts,
        _offlineRender
      );
      _nativeViewers = new ElementNoteNativeViewers(
        NativeViewerPlatform.IsSupported,
        () => NativeViewerPlatform.CreateHost(_main.RuntimePath),
        () => UiTheme.Accent,
        KeyViewerLabels.For,
        _main.LogException,
        _main.LogWarning,
        ElementNoteAppFocus.ForCurrentPlatform()
      );
      _nativeViewers.ConfigureLayouts(_main.Settings.OverlayLayouts, _main.Settings.ShowDmNoteInGame);
      _nativeViewers.EnabledChanged = () => _session?.RefreshViewers();
      _renderIpc = new KeyViewerRenderIpc();
      _counterIpc = new KeyCounterIpc();
      _ipcChannel = new ElementIpcChannel(
        _main.Path,
        _main.Version,
        _main.Log,
        new KeyLimiterIpc(new KeyLimiterSessionController(_keyLimiter)),
        _nativeViewers.Endpoint,
        _renderIpc,
        _counterIpc
      );
      _ipcChannel.Enable();
      _elementNoteApp = ElementNoteAppLauncher.Create(
        _main.RuntimePath,
        _main.Log,
        _main.LogWarning,
        _main.LogException
      );

      _settingsUi = new SettingsUiRuntime(
        _main,
        _overlay.Canvas,
        _overlay.Font,
        _nativeViewers,
        _planetAppearance.Planets,
        ApplySettings,
        ReloadSettings,
        () => _session?.RefreshViewers(),
        () => gameHitText.Applies,
        () => _playState.IsPlaying
      );
      _session = new GameplaySession(
        () => _main.Settings,
        _playState,
        _overlay,
        _nativeViewers,
        _recordingMode,
        _elementNoteApp,
        () => _settingsUi?.IsEditingLayout == true,
        ApplyLook,
        ApplyLayouts
      );
      _render = CreateRenderSession();
      _replays = new ReplayRuntime(_tufReplay, CreateReplayProviders(), _main.Log);
      _events = new GameEvents(
        _guard,
        () => _main.Settings,
        _session,
        _overlay,
        _attempts,
        _recordingMode,
        _hitTextSync,
        () => _settingsUi?.Save() == true,
        () => _settingsUi?.IsOpen == true
      );
      GameHooks.Gameplay = _events;
      GameHooks.Recording = _events;
      _applySettings = _session.ApplySettings;
      _frameLoop = new FrameLoop(
        _guard,
        // Its own guard: a failure in the settings window must not stop the
        // trackpad, the native viewers or the key limiter below.
        new FrameTask("Update the settings window", _settingsUi.Tick),
        // The first frame comes after the game loaded its own settings.
        new FrameTask(
          "Sync judgment text with ADOFAI",
          () =>
          {
            if (!_hitTextSync.Started)
              _hitTextSync.Start();
          }
        ),
        new FrameTask(
          "Move the editor with the trackpad",
          () =>
          {
            if (!InputGate.BlocksPointerInput)
              _trackpad?.Tick();
          }
        ),
        new FrameTask("Update native ElementNote viewers", _nativeViewers.Tick),
        new FrameTask("Apply the ElementNote key limiter", () => _gameKeyLimiter?.Tick()),
        new FrameTask("Join TUFReplay runs", _replays.Tick)
      );
      _initialized = true;
      ApplySettings();
      return true;
    }
    catch
    {
      Dispose();
      throw;
    }
  }

  /// <summary>UnityModManager's per-frame update, in every scene.</summary>
  public void Tick() => _frameLoop?.Tick();

  public void OpenSettings()
  {
    if (_initialized)
      _settingsUi?.Open();
  }

  public void ApplySettings()
  {
    if (!_initialized)
      return;
    // Its own guard: a failure here must not stop the rest of the settings applying.
    _guard.Run("Sync judgment text with ADOFAI", _events.PushHitTextHide);
    _guard.Run("Apply visual settings", _applySettings);
  }

  /// <summary>
  /// The live settings were replaced, such as by a profile switch: give the
  /// canvas its new elements and apply everything, layouts included.
  /// </summary>
  public void ReloadSettings()
  {
    _guard.Run(
      "Load profile layouts",
      () =>
      {
        if (!_initialized)
          return;
        _overlay?.Canvas?.ConfigureLayouts(_main.Settings.OverlayLayouts);
        _session.LayoutsReplaced();
      }
    );
    ApplySettings();
  }

  // Cheap enough for every sample of a color drag.
  private void ApplyLook()
  {
    UiTheme.SetHighlight(_main.Settings.HighlightColor);
    _overlay?.Fonts?.SetDefault(_main.Settings.OverlayFont);
    ChatterBlockerOptions chatter = _main.Settings.Gameplay.Chatter;
    _main.Chatter.Configure(chatter.Enabled, chatter.WindowMs, chatter.IgnoredKeys);
    _editorTweaks?.Apply();
    _planetAppearance?.Apply();
    _effects?.ApplySettings();
  }

  private void ApplyLayouts()
  {
    _overlay?.Canvas?.ApplyLayouts();
    _nativeViewers?.ConfigureLayouts(_main.Settings.OverlayLayouts);
  }

  public void Dispose()
  {
    if (_disposed)
      return;

    _disposed = true;
    _initialized = false;
    _guard.Stop();
    // The patches stay until the end; from now on they leave the game as it is.
    GameHooks.Clear();
    _frameLoop = null;

    // TUFReplay stops calling Element before anything it uses goes away.
    _replays?.Dispose();
    _replays = null;
    // ElementNote counts the player's keys again even if a replay is still running.
    _counterIpc?.EndReplay();

    // Before the canvases and the IPC channel it uses go away.
    _render?.Dispose();
    _render = null;

    _settingsUi?.Dispose();
    _settingsUi = null;

    _trackpad?.Dispose();
    _trackpad = null;
    // The chatter blocker outlives the mod; its Tweaks setting stops with it.
    if (_editorTweaks != null)
      _main.Chatter.Configure(false, 0, null);
    _editorTweaks?.Dispose();
    _editorTweaks = null;
    _planetAppearance?.Dispose();
    _planetAppearance = null;
    _effects?.Dispose();
    _effects = null;

    _session = null;
    _nativeViewers?.Dispose();
    _nativeViewers = null;

    _ipcChannel?.Dispose();
    _ipcChannel = null;
    _keyLimiter?.Clear();
    _keyLimiter = null;
    // Gives the game the player's own saved keys back. The guard has stopped
    // by now, so this runs on its own.
    try
    {
      _gameKeyLimiter?.Dispose();
    }
    catch (Exception exception)
    {
      _main.LogException("Restore the game key limiter", exception);
    }
    _gameKeyLimiter = null;

    _recordingMode?.Dispose();
    _recordingMode = null;
    _hitTextSync = null;

    if (_sceneHooked)
    {
      SceneManager.sceneUnloaded -= OnSceneUnloaded;
      _sceneHooked = false;
    }

    _overlay?.Dispose();
    _overlay = null;
    _tufReplay?.Dispose();
    _tufReplay = null;

    // Ends the play in progress and waits briefly for its save.
    try
    {
      _attempts?.Dispose();
    }
    catch (Exception exception)
    {
      _main.LogException("Save attempts", exception);
    }
    _attempts = null;
    _attemptRuns?.Dispose();
    _attemptRuns = null;

    _harmony?.UnpatchAll(HarmonyId);
    _harmony = null;
  }

  private void OnSceneUnloaded(Scene scene) => _events?.OnSceneUnloaded();

  // Without a render runtime, Element stays out of rendered replays but keeps its run states.
  private KeyValuePair<string, object>[] CreateReplayProviders()
  {
    var providers = new List<KeyValuePair<string, object>>(3)
    {
      new(
        AttemptRunStateProvider.Id,
        new AttemptRunStateProvider(_attempts, _replayedAttempts, () => _overlay?.RefreshAll())
      ),
      new(KeyCountRunStateProvider.Id, new KeyCountRunStateProvider(_counterIpc, _recordedKeyCounts)),
    };
    if (_render != null)
      providers.Add(new(ReplayRenderProvider.Id, new ReplayRenderProvider(_render)));
    return providers.ToArray();
  }

  // Render support is optional: a failure here leaves the rest of the mod running.
  private RenderSession CreateRenderSession()
  {
    try
    {
      return new RenderSession(
        _tufReplay,
        _offlineRender,
        _renderIpc,
        _recordedKeyCounts,
        () => _overlay?.Canvas?.Root != null ? _overlay.Canvas.Root.GetComponent<Canvas>() : null,
        // The video shows the key viewers the game would show during this play.
        (width, height) =>
          _session?.ShowsKeyViewersInPlay == true
            ? _nativeViewers.RenderPlacements(width, height)
            : Array.Empty<RenderViewerPlacement>(),
        () => _nativeViewers?.Availability is KeyViewerAvailability.Ready or KeyViewerAvailability.HiddenInApp,
        AttemptCounter.Rules,
        failure =>
        {
          (string title, string detail) = RenderNotices.For(failure.Code, UiLocale.Korean);
          _settingsUi?.ShowNotice(title, detail);
        },
        _main.LogWarning
      );
    }
    catch (Exception exception)
    {
      _main.LogException("Render support is off because it could not start", exception);
      return null;
    }
  }

  // Counting is optional: a failure here leaves the rest of the mod running.
  private AttemptTracker CreateAttemptTracker()
  {
    try
    {
      _attemptRuns = new AttemptRunReader();
      return new AttemptTracker(
        _attemptRuns,
        new AttemptFileStore(_main.Path, _main.LogException, _main.LogWarning),
        AttemptCounter.Rules,
        _main.LogException
      );
    }
    catch (Exception exception)
    {
      _main.LogException("Attempt counting is off because it could not start", exception);
      _attemptRuns?.Dispose();
      _attemptRuns = null;
      return null;
    }
  }

  // Game versions before ADOFAI's built-in key limiter have no KeysSetting;
  // the rest of the mod keeps working without the key limiter.
  private GameKeyLimiter CreateGameKeyLimiter(KeyLimiterService profiles)
  {
    try
    {
      return new GameKeyLimiter(
        () => profiles.Current,
        () => KeyLimiterMenuText.Label,
        () => KeyLimiterMenuText.Description
      );
    }
    catch (Exception exception)
    {
      _main.LogException("Key limiter is off because this game version has no built-in key limiter", exception);
      return null;
    }
  }
}
