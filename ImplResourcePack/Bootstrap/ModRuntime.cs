using System;
using System.Collections.Generic;
using HarmonyLib;
using ImplResourcePack.Application.Attempts;
using ImplResourcePack.Application.KeyLimiter;
using ImplResourcePack.Domain.Judgement;
using ImplResourcePack.Infrastructure.Attempts;
using ImplResourcePack.Infrastructure.DmNoteApp;
using ImplResourcePack.Infrastructure.Game.Attempts;
using ImplResourcePack.Infrastructure.Game.DmNote;
using ImplResourcePack.Infrastructure.Game.Editor;
using ImplResourcePack.Infrastructure.Game.Effects;
using ImplResourcePack.Infrastructure.Game.Input;
using ImplResourcePack.Infrastructure.Game.Recording;
using ImplResourcePack.Infrastructure.Game.Tweaks;
using ImplResourcePack.Infrastructure.Ipc;
using ImplResourcePack.Presentation.Overlay.Shared;
using ImplResourcePack.Presentation.Settings;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ImplResourcePack.Bootstrap;

/// <summary>
/// Composes the mod: patches, input limiting, IPC and gameplay tweaks here; the
/// overlay and the settings UI in their own runtimes. Game callbacks enter
/// through this facade, which isolates their failures from the game.
/// </summary>
internal sealed class ModRuntime : IDisposable
{
  private const string HarmonyId = "ImplResourcePack";

  private readonly Main _main;
  private readonly Dictionary<string, int> _failures = new(StringComparer.Ordinal);
  private Harmony _harmony;
  private KeyLimiterService _keyLimiter;
  private GameKeyLimiter _gameKeyLimiter;
  private KeyLimiterIpcFeature _keyLimiterIpc;
  private DmNoteNativeIpcFeature _nativeViewers;
  private DmNoteAppLauncher _dmNoteApp;
  private bool _dmNoteAutoOpen;
  private RecordingModeController _recordingMode;
  private EditorTrackpadController _trackpad;
  private GameplayTweaksController _gameplayTweaks;
  private EffectsController _effects;
  private AttemptRunReader _attemptRuns;
  private AttemptTracker _attempts;
  private OverlayRuntime _overlay;
  private SettingsUiRuntime _settingsUi;
  private bool _sceneHooked;
  private bool _initialized;
  private bool _disposed;
  private bool _presentationSettingsApplied;

  // Per-frame and per-hit callbacks reuse these delegates; a lambda per call
  // would add garbage, and every GC pause also stops the async input thread.
  private readonly Action _tickNativeViewers;
  private readonly Action _tickKeyLimiter;
  private readonly Action<scrMarginTracker, HitMargin> _registerHit;
  private readonly Action _refreshAccuracy;
  private readonly Action _refreshFloor;
  private readonly Action<int> _refreshTime;
  private readonly Action<scrMissIndicator> _hideMissIndicator;
  private readonly Action _releaseFallbackGlyphs;

  internal KeyLimiterService KeyLimiter => _keyLimiter;
  internal IReadOnlyList<string> FailedPatchGroups { get; private set; } = Array.Empty<string>();
  public bool OverlaysAvailable => _overlay?.Available == true;
  public bool IsRecording => _recordingMode?.IsActive ?? false;

  public ModRuntime(Main main)
  {
    _main = main;
    _tickNativeViewers = () => _nativeViewers?.Tick();
    _tickKeyLimiter = () => _gameKeyLimiter?.Tick();
    _registerHit = (tracker, hit) => _overlay?.RegisterHit(tracker, hit);
    _refreshAccuracy = () => _overlay?.RefreshAccuracy();
    _refreshFloor = () =>
    {
      _attempts?.MarkPlayed();
      _overlay?.RefreshFloor();
    };
    _refreshTime = framesPerSecond => _overlay?.RefreshTime(framesPerSecond);
    _hideMissIndicator = indicator => _recordingMode?.HideMissIndicator(indicator);
    _releaseFallbackGlyphs = () => _overlay?.ReleaseFallbackGlyphs();
  }

  public bool Initialize()
  {
    if (_initialized)
      return true;
    if (_disposed)
      throw new ObjectDisposedException(nameof(ModRuntime));

    try
    {
      _keyLimiter = new KeyLimiterService(new DmNoteKeyResolver());
      _gameKeyLimiter = CreateGameKeyLimiter(_keyLimiter);
      _keyLimiterIpc = new KeyLimiterIpcFeature(
        new KeyLimiterSessionController(_keyLimiter),
        _main.Path,
        _main.Version,
        _main.Log
      );

      _harmony = new Harmony(HarmonyId);
      FailedPatchGroups = PatchGroups.Apply(_harmony, typeof(Main).Assembly, _main.LogException);
      _trackpad = EditorTrackpadController.TryCreate(_main);
      _gameplayTweaks = new GameplayTweaksController(_main);
      _effects = new EffectsController(() => _main.Settings.Effects);
      _recordingMode = new RecordingModeController();
      SceneManager.sceneUnloaded += OnSceneUnloaded;
      _sceneHooked = true;
      WarnIfKeyboardChatterBlockerIsLoaded();

      _attempts = CreateAttemptTracker();
      _overlay = new OverlayRuntime(_main, OnTimeTick, _attempts);
      _nativeViewers = new DmNoteNativeIpcFeature(
        NativeViewerPlatform.IsSupported,
        () => NativeViewerPlatform.CreateHost(_main.RuntimePath),
        () => OverlayTheme.Accent,
        _main.LogException,
        _main.LogWarning
      );
      _nativeViewers.ConfigureLayouts(_main.Settings.OverlayLayouts, _main.Settings.ShowDmNoteInGame);
      _nativeViewers.EnabledChanged = () => RefreshViewerMode(IsGameplayActive());
      _keyLimiterIpc.AttachNativeViewers(_nativeViewers);
      _keyLimiterIpc.Enable();
      _dmNoteApp = DmNoteAppLauncher.Create(_main.RuntimePath, _main.Log, _main.LogWarning, _main.LogException);

      _settingsUi = new SettingsUiRuntime(
        _main,
        _overlay.Canvas,
        _overlay.Font,
        _nativeViewers,
        _gameplayTweaks.Planets,
        ApplySettings,
        () => RefreshViewerMode(IsGameplayActive())
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

  /// <summary>A play started from <paramref name="startFloor"/>: count it, then show the overlay.</summary>
  public void BeginGameplay(int startFloor)
  {
    Execute("Count attempt", () => _attempts?.Begin(startFloor));
    ShowGameplay();
  }

  public void ShowGameplay()
  {
    Execute(
      "Show overlay",
      () =>
      {
        if (!_initialized || _overlay == null)
          return;
        if (IsRecording)
        {
          HideCore();
          _recordingMode.ReapplyCurrentScene();
          return;
        }
        if (scrController.coopMode)
        {
          HideCore();
          return;
        }

        RefreshViewerMode(true);
        _overlay.Show();
      }
    );
  }

  /// <summary>UnityModManager's per-frame update, in every scene.</summary>
  public void TickTrackpad()
  {
    if (_initialized)
      _settingsUi?.Tick();
    if (!UIInteractionState.BlocksPointerInput)
      _trackpad?.Tick();
    Execute("Update native ImplDmNote viewers", _tickNativeViewers);
    Execute("Apply the ImplDmNote key limiter", _tickKeyLimiter);
  }

  public void OpenSettings()
  {
    if (_initialized)
      _settingsUi?.Open();
  }

  /// <summary>The play ended or its scene is going away.</summary>
  public void HideGameplay()
  {
    Execute("Finish attempt", () => _attempts?.End());
    Execute("Hide overlay", HideCore);
  }

  public void ApplySettings()
  {
    Execute(
      "Apply visual settings",
      () =>
      {
        if (!_initialized)
          return;

        OpenDmNoteWhenEnabled();
        OverlayTheme.SetHighlight(_main.Settings.HighlightColor);
        _gameplayTweaks?.Apply();
        _effects?.ApplySettings();
        // Color drags only update the affected planet. Avoid rebuilding overlay
        // layouts, refreshing all counters or scanning the recording HUD per sample.
        if (_presentationSettingsApplied && IsRecording == _main.Settings.RecordMode)
          return;
        _presentationSettingsApplied = true;
        _overlay?.Canvas?.ApplyLayouts();
        _nativeViewers?.ConfigureLayouts(_main.Settings.OverlayLayouts);
        _recordingMode?.Apply(_main.Settings.RecordMode);
        if (IsRecording)
        {
          HideCore();
          return;
        }

        if (IsGameplayActive())
          ShowGameplay();
        else
          RefreshViewerMode(false);
      }
    );
  }

  // Opens once at startup and again when the option is switched on; settings
  // changes such as color drags do not start the app check again.
  private void OpenDmNoteWhenEnabled()
  {
    bool enabled = _main.Settings.AutoOpenCloseDmNote;
    if (enabled && !_dmNoteAutoOpen)
      _dmNoteApp?.OpenIfClosed();
    _dmNoteAutoOpen = enabled;
  }

  public bool ShouldShowHitText(HitMargin hitMargin)
  {
    return JudgementVisualPolicy.ShouldShow(_main.Settings.Gameplay.Judgement, IsRecording, (int)hitMargin);
  }

  public void ReapplyRecordingUi()
  {
    Execute("Reapply recording UI", () => _recordingMode?.ReapplyCurrentScene());
  }

  public void HideRecordingMissIndicator(scrMissIndicator indicator)
  {
    Execute("Hide recording miss indicator", _hideMissIndicator, indicator);
  }

  public void HideRecordingErrorMeter()
  {
    Execute("Hide recording error meter", () => _recordingMode?.HideErrorMeter());
  }

  public void OnHit(scrMarginTracker tracker, HitMargin hit)
  {
    Execute("Update judgement and combo", _registerHit, tracker, hit);
  }

  public void OnTrackerReset(scrMarginTracker tracker)
  {
    Execute("Refresh overlay after reset", () => _overlay?.RefreshAfterReset(tracker));
  }

  public void OnPlayerDied(scrMarginTracker tracker)
  {
    Execute(
      "Refresh overlay after death",
      () =>
      {
        _attempts?.MarkPlayed();
        _overlay?.RefreshAfterDeath(tracker);
      }
    );
  }

  public void OnAccuracyChanged()
  {
    Execute("Update XAccuracy", _refreshAccuracy);
  }

  public void OnFloorChanged()
  {
    Execute("Update progress, BPM and timing scale", _refreshFloor);
  }

  public void Dispose()
  {
    if (_disposed)
      return;

    _disposed = true;
    _initialized = false;

    _settingsUi?.Dispose();
    _settingsUi = null;

    _trackpad?.Dispose();
    _trackpad = null;
    _gameplayTweaks?.Dispose();
    _gameplayTweaks = null;
    _effects?.Dispose();
    _effects = null;

    _nativeViewers?.Dispose();
    _nativeViewers = null;

    _keyLimiterIpc?.Dispose();
    _keyLimiterIpc = null;
    _keyLimiter?.Clear();
    _keyLimiter = null;
    // Gives the game the player's own saved keys back.
    Execute("Restore the game key limiter", () => _gameKeyLimiter?.Dispose());
    _gameKeyLimiter = null;

    _recordingMode?.Dispose();
    _recordingMode = null;

    if (_sceneHooked)
    {
      SceneManager.sceneUnloaded -= OnSceneUnloaded;
      _sceneHooked = false;
    }

    _overlay?.Dispose();
    _overlay = null;

    // Ends the play in progress and waits briefly for its save. Not through
    // Execute: it skips everything once the runtime is disposed.
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

  private void OnTimeTick(int framesPerSecond)
  {
    Execute("Update status time", _refreshTime, framesPerSecond);
  }

  private void OnSceneUnloaded(Scene scene)
  {
    _recordingMode?.ForgetDestroyedSceneObjects();
    HideGameplay();
    // Overlay texts are inactive now, so they rebuild their glyphs on the next
    // show. Open settings texts share the font and would keep blank glyphs.
    if (_settingsUi?.IsOpen != true)
      Execute("Release overlay fallback glyphs", _releaseFallbackGlyphs);
  }

  private void HideCore()
  {
    RefreshViewerMode(false);
    _overlay?.Hide();
  }

  private void RefreshViewerMode(bool gameplay)
  {
    if (_nativeViewers == null)
      return;
    bool supported = NativeViewerPlatform.IsSupported;
    bool editing = _settingsUi?.IsEditingLayout == true;
    bool enabled =
      supported && _nativeViewers.HasEnabledViewers && (editing || (!IsRecording && !scrController.coopMode));
    _nativeViewers.SetActive(enabled && (gameplay || editing));
  }

  private void Execute(string context, Action action)
  {
    if (_disposed)
      return;

    try
    {
      action();
    }
    catch (Exception exception)
    {
      ReportFailure(context, exception);
    }
  }

  private void Execute<T>(string context, Action<T> action, T argument)
  {
    if (_disposed)
      return;

    try
    {
      action(argument);
    }
    catch (Exception exception)
    {
      ReportFailure(context, exception);
    }
  }

  private void Execute<T1, T2>(string context, Action<T1, T2> action, T1 first, T2 second)
  {
    if (_disposed)
      return;

    try
    {
      action(first, second);
    }
    catch (Exception exception)
    {
      ReportFailure(context, exception);
    }
  }

  // Per-frame callbacks can fail every frame. Log the first stack trace, then
  // only a short count so a persistent error cannot flood the log.
  private void ReportFailure(string context, Exception exception)
  {
    _failures.TryGetValue(context, out int count);
    _failures[context] = ++count;
    if (count == 1)
      _main.LogException(context, exception);
    else if (count == 10 || count == 100 || count % 1000 == 0)
      _main.LogWarning(
        "[" + context + "] failed " + count + " times. Latest: " + exception.GetType().Name + ": " + exception.Message
      );
  }

  private static bool IsGameplayActive()
  {
    return ADOBase.controller != null
      && !ADOBase.controller.paused
      && ADOBase.conductor != null
      && ADOBase.conductor.isGameWorld;
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
      return new GameKeyLimiter(() => profiles.Current);
    }
    catch (Exception exception)
    {
      _main.LogException("Key limiter is off because this game version has no built-in key limiter", exception);
      return null;
    }
  }

  private void WarnIfKeyboardChatterBlockerIsLoaded()
  {
    foreach (System.Reflection.Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
    {
      if (!string.Equals(assembly.GetName().Name, "KeyboardChatterBlocker", StringComparison.OrdinalIgnoreCase))
        continue;

      _main.LogWarning(
        "[Input] KeyboardChatterBlocker is loaded. ADOFAI's key limiter now carries ImplDmNote's keys, and Tweaks > Chatter blocker replaces its chatter filter; turn its limiter and chatter filter off to avoid filtering input twice."
      );
      return;
    }
  }
}
