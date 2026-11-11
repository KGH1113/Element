using System;
using Element.Application.Attempts;
using Element.Application.Gameplay;
using Element.Application.Judgement;
using Element.Bootstrap.Composition;
using Element.Configuration;
using Element.Domain.Judgement;
using Element.Infrastructure.Game.Patches;
using Element.Infrastructure.Game.Recording;

namespace Element.Bootstrap.Hosting;

/// <summary>
/// What the game tells the mod, through its patches and its scenes, handed to the parts
/// that follow it. Every call runs under the failure guard, so a failure here never
/// reaches the game. The delegates are made once: hits and frames call these often.
/// </summary>
internal sealed class GameEvents : IGameplayHooks, IRecordingHooks
{
  private readonly FailureGuard _guard;
  private readonly Func<ElementSettings> _settings;
  private readonly RecordingModeController _recording;
  private readonly Func<bool> _settingsOpen;

  private readonly Action<int> _beginAttempt;
  private readonly Action _endAttempt;
  private readonly Action _show;
  private readonly Action _hide;
  private readonly Action<scrMarginTracker, HitMargin> _registerHit;
  private readonly Action<scrMarginTracker> _afterReset;
  private readonly Action<scrMarginTracker> _afterDeath;
  private readonly Action _refreshAccuracy;
  private readonly Action _refreshFloor;
  private readonly Action<int> _refreshTime;
  private readonly Action _reapplyRecordingUi;
  private readonly Action _hideErrorMeter;
  private readonly Action<scrMissIndicator> _hideMissIndicator;
  private readonly Action _pullHitTextHide;
  private readonly Action _pushHitTextHide;
  private readonly Action _releaseFallbackGlyphs;

  /// <param name="attempts">Null when attempt counting could not start.</param>
  /// <param name="save">Saves the settings; false when they could not be written.</param>
  /// <param name="settingsOpen">The settings window or the layout editor is open.</param>
  public GameEvents(
    FailureGuard guard,
    Func<ElementSettings> settings,
    GameplaySession session,
    OverlayRuntime overlay,
    AttemptTracker attempts,
    RecordingModeController recording,
    HitTextHideSync hitTextSync,
    Func<bool> save,
    Func<bool> settingsOpen
  )
  {
    _guard = guard;
    _settings = settings;
    _recording = recording;
    _settingsOpen = settingsOpen;
    _beginAttempt = startFloor => attempts?.Begin(startFloor);
    _endAttempt = () => attempts?.End();
    _show = session.Show;
    _hide = session.Hide;
    _registerHit = overlay.RegisterHit;
    _afterReset = overlay.RefreshAfterReset;
    _afterDeath = tracker =>
    {
      attempts?.MarkPlayed();
      overlay.RefreshAfterDeath(tracker);
    };
    _refreshAccuracy = overlay.RefreshAccuracy;
    _refreshFloor = () =>
    {
      attempts?.MarkPlayed();
      overlay.RefreshFloor();
    };
    _refreshTime = overlay.RefreshTime;
    _reapplyRecordingUi = recording.ReapplyCurrentScene;
    _hideErrorMeter = recording.HideErrorMeter;
    _hideMissIndicator = recording.HideMissIndicator;
    _pullHitTextHide = hitTextSync.PullFromGame;
    // ADOFAI saves its Hide Judgment Text at once; the mod's choice behind it
    // is saved with it, not only when the settings window closes.
    _pushHitTextHide = () =>
    {
      if (hitTextSync.Push())
        save();
    };
    _releaseFallbackGlyphs = overlay.ReleaseFallbackGlyphs;
  }

  /// <summary>Sends the mod's Hide Judgment Text choice to ADOFAI and saves it with the game's.</summary>
  public Action PushHitTextHide => _pushHitTextHide;

  public void BeginGameplay(int startFloor)
  {
    _guard.Run("Count attempt", _beginAttempt, startFloor);
    _guard.Run("Show overlay", _show);
  }

  public void HideGameplay()
  {
    _guard.Run("Finish attempt", _endAttempt);
    _guard.Run("Hide overlay", _hide);
  }

  public void OnHit(scrMarginTracker tracker, HitMargin hit) =>
    _guard.Run("Update judgement and combo", _registerHit, tracker, hit);

  public void OnTrackerReset(scrMarginTracker tracker) =>
    _guard.Run("Refresh overlay after reset", _afterReset, tracker);

  public void OnPlayerDied(scrMarginTracker tracker) => _guard.Run("Refresh overlay after death", _afterDeath, tracker);

  public void OnAccuracyChanged() => _guard.Run("Update XAccuracy", _refreshAccuracy);

  public void OnFloorChanged() => _guard.Run("Update progress, BPM and timing scale", _refreshFloor);

  public bool ShouldShowHitText(HitMargin hitMargin) =>
    JudgementVisualPolicy.ShouldShow(_settings().Gameplay.Judgement, _recording.IsActive, (int)hitMargin);

  /// <summary>The overlay's clock ticked.</summary>
  public void OnTimeTick(int framesPerSecond) => _guard.Run("Update status time", _refreshTime, framesPerSecond);

  /// <summary>The player changed Hide Judgment Text in the game's pause settings.</summary>
  public void OnGameHitTextHideChanged() => _guard.Run("Sync judgment text from ADOFAI", _pullHitTextHide);

  /// <summary>Competitive Mode decides whether the game applies Hide Judgment Text.</summary>
  public void OnCompetitiveModeChanged() => _guard.Run("Sync judgment text with ADOFAI", _pushHitTextHide);

  /// <summary>A scene is going away: the play ends with it.</summary>
  public void OnSceneUnloaded()
  {
    _recording.ForgetDestroyedSceneObjects();
    HideGameplay();
    // Overlay texts are inactive now, so they rebuild their glyphs on the next
    // show. Open settings texts share the font and would keep blank glyphs.
    if (!_settingsOpen())
      _guard.Run("Release overlay fallback glyphs", _releaseFallbackGlyphs);
  }

  public bool IsRecording => _recording.IsActive;

  public bool SuppressNextFlash
  {
    get => _recording.SuppressNextFlash;
    set => _recording.SuppressNextFlash = value;
  }

  public void ReapplyUi() => _guard.Run("Reapply recording UI", _reapplyRecordingUi);

  public void HideErrorMeter() => _guard.Run("Hide recording error meter", _hideErrorMeter);

  public void HideMissIndicator(scrMissIndicator indicator) =>
    _guard.Run("Hide recording miss indicator", _hideMissIndicator, indicator);
}
