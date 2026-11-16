namespace Element.Infrastructure.Game.Patches;

/// <summary>
/// A play as the game patches see it. Its handler keeps a failure from reaching
/// the game, which would stop the frame or the hit that called it.
/// </summary>
internal interface IGameplayHooks
{
  /// <summary>A play started from <paramref name="startFloor"/>.</summary>
  void BeginGameplay(int startFloor);

  /// <summary>The play ended or its scene is going away.</summary>
  void HideGameplay();

  void OnHit(scrMarginTracker tracker, HitMargin hit);

  /// <summary>The tracker restarted or went back to a checkpoint.</summary>
  void OnTrackerReset(scrMarginTracker tracker);

  void OnPlayerDied(scrMarginTracker tracker);

  void OnAccuracyChanged();

  void OnFloorChanged();

  /// <summary>Whether the game may show its judgment text for <paramref name="hitMargin"/>.</summary>
  bool ShouldShowHitText(HitMargin hitMargin);
}
