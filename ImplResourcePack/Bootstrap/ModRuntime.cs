using System;
using HarmonyLib;
using ImplResourcePack.Application;
using ImplResourcePack.Application.Features;
using ImplResourcePack.Application.KeyLimiter;
using ImplResourcePack.Domain.Judgement;
using ImplResourcePack.Infrastructure.Assets;
using ImplResourcePack.Infrastructure.Game.Bpm;
using ImplResourcePack.Infrastructure.Game.Editor;
using ImplResourcePack.Infrastructure.Game.Judgement;
using ImplResourcePack.Infrastructure.Game.Recording;
using ImplResourcePack.Infrastructure.Game.Status;
using ImplResourcePack.Infrastructure.Game.TimingScale;
using ImplResourcePack.Infrastructure.Ipc;
using ImplResourcePack.Presentation.Overlay.Bpm;
using ImplResourcePack.Presentation.Overlay.Combo;
using ImplResourcePack.Presentation.Overlay.Judgement;
using ImplResourcePack.Presentation.Overlay.Shared;
using ImplResourcePack.Presentation.Overlay.Status;
using ImplResourcePack.Presentation.Overlay.TimingScale;
using UnityEngine.SceneManagement;

namespace ImplResourcePack.Bootstrap;

internal sealed class ModRuntime : IDisposable
{
  private const string HarmonyId = "ImplResourcePack";

  private readonly Main _main;
  private FontBundleLoader _fontBundleLoader;
  private OverlayCanvasHost _canvasHost;
  private OverlayMaterialFactory _materialFactory;
  private OverlayCoordinator _coordinator;
  private StatusTimeTicker _timeTicker;
  private Harmony _harmony;
  private KeyLimiterService _keyLimiter;
  private KeyLimiterIpcFeature _keyLimiterIpc;
  private RecordingModeController _recordingMode;
  private bool _sceneHooked;
  private bool _initialized;
  private bool _disposed;
  private EditorTrackpadController _trackpad;

  internal KeyLimiterService KeyLimiter => _keyLimiter;
  public bool OverlaysAvailable => _coordinator != null;
  public bool IsRecording => _recordingMode?.IsActive ?? false;

  public ModRuntime(Main main)
  {
    _main = main;
  }

  public bool Initialize()
  {
    if (_initialized)
      return true;
    if (_disposed)
      throw new ObjectDisposedException(nameof(ModRuntime));

    try
    {
      _keyLimiter = new KeyLimiterService();
      _keyLimiterIpc = new KeyLimiterIpcFeature(_keyLimiter);
      _keyLimiterIpc.Enable();

      _harmony = new Harmony(HarmonyId);
      _harmony.PatchAll(typeof(Main).Assembly);
      _trackpad = EditorTrackpadController.TryCreate(_main);
      _recordingMode = new RecordingModeController();
      SceneManager.sceneUnloaded += OnSceneUnloaded;
      _sceneHooked = true;
      WarnIfKeyboardChatterBlockerIsLoaded();

      _fontBundleLoader = new FontBundleLoader(_main);
      if (!_fontBundleLoader.Load())
      {
        _fontBundleLoader.Dispose();
        _fontBundleLoader = null;
        _initialized = true;
        ApplySettings();
        return true;
      }

      _canvasHost = new OverlayCanvasHost();
      _materialFactory = new OverlayMaterialFactory(_fontBundleLoader.FontAsset);

      JudgementFeature judgement = new(
        new JudgementReader(),
        new JudgementView(_canvasHost, _fontBundleLoader.FontAsset, _materialFactory.Material)
      );
      StatusFeature status = new(
        new StatusReader(),
        new StatusView(_canvasHost, _fontBundleLoader.FontAsset, _materialFactory.Material)
      );
      BpmFeature bpm = new(
        new BpmReader(),
        new BpmView(_canvasHost, _fontBundleLoader.FontAsset, _materialFactory.Material)
      );
      ComboFeature combo = new(new ComboView(_canvasHost, _fontBundleLoader.FontAsset, _materialFactory.Material));
      TimingScaleFeature timingScale = new(
        new TimingScaleReader(),
        new TimingScaleView(_canvasHost, _fontBundleLoader.FontAsset, _materialFactory.Material)
      );
      _coordinator = new OverlayCoordinator(judgement, status, bpm, combo, timingScale);

      _timeTicker = _canvasHost.Root.AddComponent<StatusTimeTicker>();
      _timeTicker.Initialize(OnTimeTick);

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

  public void ShowGameplay()
  {
    Execute(
      "Show overlay",
      () =>
      {
        if (!_initialized || !OverlaysAvailable)
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

        _canvasHost.SetVisible(true);
        _coordinator.Show();
        _timeTicker.StartTicking();
      }
    );
  }

  public void TickTrackpad() => _trackpad?.Tick();

  public void HideGameplay()
  {
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

        _recordingMode?.Apply(_main.Settings.RecordMode);
        if (IsRecording)
        {
          HideCore();
          return;
        }

        if (IsGameplayActive())
          ShowGameplay();
      }
    );
  }

  public bool ShouldShowHitText(HitMargin hitMargin)
  {
    return HitTextVisibilityPolicy.ShouldShow(
      _main.Settings.HidePerfectJudgmentText,
      IsRecording,
      HitMarginPolicy.IsPerfect(hitMargin)
    );
  }

  public void ReapplyRecordingUi()
  {
    Execute("Reapply recording UI", () => _recordingMode?.ReapplyCurrentScene());
  }

  public void HideRecordingMissIndicator(scrMissIndicator indicator)
  {
    Execute("Hide recording miss indicator", () => _recordingMode?.HideMissIndicator(indicator));
  }

  public void HideRecordingErrorMeter()
  {
    Execute("Hide recording error meter", () => _recordingMode?.HideErrorMeter());
  }

  public void OnHit(scrMarginTracker tracker, HitMargin hit)
  {
    Execute("Update judgement and combo", () => _coordinator?.RegisterHit(tracker, hit));
  }

  public void OnTrackerReset(scrMarginTracker tracker)
  {
    Execute(
      "Refresh overlay after reset",
      () =>
      {
        _coordinator?.RefreshAfterReset(tracker);
        if (_coordinator is { IsVisible: true })
          _timeTicker?.StartTicking();
      }
    );
  }

  public void OnPlayerDied(scrMarginTracker tracker)
  {
    Execute(
      "Refresh overlay after death",
      () =>
      {
        _timeTicker?.StopTicking();
        _coordinator?.RefreshAfterReset(tracker);
      }
    );
  }

  public void OnAccuracyChanged()
  {
    Execute("Update XAccuracy", () => _coordinator?.RefreshAccuracy());
  }

  public void OnFloorChanged()
  {
    Execute("Update progress, BPM and timing scale", () => _coordinator?.RefreshFloor());
  }

  public void Dispose()
  {
    if (_disposed)
      return;

    _disposed = true;
    _initialized = false;

    _trackpad?.Dispose();
    _trackpad = null;

    _keyLimiterIpc?.Dispose();
    _keyLimiterIpc = null;
    _keyLimiter?.Clear();
    _keyLimiter = null;

    _recordingMode?.Dispose();
    _recordingMode = null;

    if (_sceneHooked)
    {
      SceneManager.sceneUnloaded -= OnSceneUnloaded;
      _sceneHooked = false;
    }

    _timeTicker?.StopTicking();
    _coordinator?.Hide();
    _canvasHost?.SetVisible(false);

    _coordinator?.Dispose();
    _coordinator = null;
    _timeTicker = null;

    _canvasHost?.Dispose();
    _canvasHost = null;

    _materialFactory?.Dispose();
    _materialFactory = null;

    _fontBundleLoader?.Dispose();
    _fontBundleLoader = null;

    _harmony?.UnpatchAll(HarmonyId);
    _harmony = null;
  }

  private void OnTimeTick(int framesPerSecond)
  {
    Execute("Update status time", () => _coordinator?.RefreshTime(framesPerSecond));
  }

  private void OnSceneUnloaded(Scene scene)
  {
    _recordingMode?.ForgetDestroyedSceneObjects();
    HideGameplay();
  }

  private void HideCore()
  {
    _timeTicker?.StopTicking();
    _coordinator?.Hide();
    _canvasHost?.SetVisible(false);
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
      _main.LogException(context, exception);
    }
  }

  private static bool IsGameplayActive()
  {
    return ADOBase.controller != null
      && !ADOBase.controller.paused
      && ADOBase.conductor != null
      && ADOBase.conductor.isGameWorld;
  }

  private void WarnIfKeyboardChatterBlockerIsLoaded()
  {
    foreach (System.Reflection.Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
    {
      if (!string.Equals(assembly.GetName().Name, "KeyboardChatterBlocker", StringComparison.OrdinalIgnoreCase))
        continue;

      _main.LogWarning(
        "[KeyLimiter] KeyboardChatterBlocker is loaded. Disable its key limiter to avoid combined filtering."
      );
      return;
    }
  }
}
