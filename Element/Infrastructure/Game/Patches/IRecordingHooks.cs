namespace Element.Infrastructure.Game.Patches;

/// <summary>Record mode's hidden game UI, as the game patches see it.</summary>
internal interface IRecordingHooks
{
  bool IsRecording { get; }

  /// <summary>The flash of the level's last floor is skipped once while recording.</summary>
  bool SuppressNextFlash { get; set; }

  /// <summary>The game rebuilt UI that record mode hides.</summary>
  void ReapplyUi();

  void HideErrorMeter();

  void HideMissIndicator(scrMissIndicator indicator);
}
