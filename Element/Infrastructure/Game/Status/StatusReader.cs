using System;
using Element.Application;
using Element.Domain.Status;
using UnityEngine;

namespace Element.Infrastructure.Game.Status;

internal sealed class StatusReader : IStatusSource
{
  private bool _songStarted;
  private float _startProgress;

  public void ResetRunState()
  {
    _songStarted = false;
    _startProgress = ReadCurrentProgress();
  }

  public StatusSnapshot ReadMetrics(StatusSnapshot current)
  {
    scrController controller = scrController.instance;
    scrPlayerManager playerManager = scrPlayerManager.instance;
    if (controller == null || playerManager == null || playerManager.mistakesManager == null)
      return current;

    float progress = Mathf.Clamp01(controller.percentComplete);
    float xAccuracy = playerManager.mistakesManager.percentXAcc;
    if (float.IsNaN(xAccuracy) || float.IsInfinity(xAccuracy))
      xAccuracy = 1f;

    return current.WithMetrics(_startProgress, progress, Mathf.Clamp01(xAccuracy), RDString.language);
  }

  public StatusSnapshot ReadTime(StatusSnapshot current, int? framesPerSecond = null)
  {
    if (framesPerSecond.HasValue)
      current = current.WithFps(true, framesPerSecond.Value);

    scrController controller = scrController.instance;
    scrConductor conductor = scrConductor.instance;
    if (controller == null || conductor == null)
      return current;

    AudioSource song = conductor.song;
    if (song != null && song.clip != null)
    {
      int totalSeconds = Math.Max(0, (int)song.clip.length);
      int currentSeconds = Math.Max(0, (int)song.time);

      if (song.time > 0f)
        _songStarted = true;
      else if (_songStarted)
        currentSeconds = totalSeconds;

      currentSeconds = Math.Min(currentSeconds, totalSeconds);
      return current.WithTime(false, currentSeconds, totalSeconds, RDString.language);
    }

    scrLevelMaker levelMaker = scrLevelMaker.instance;
    if (levelMaker == null || levelMaker.listFloors == null || levelMaker.listFloors.Count == 0)
      return current;

    double totalTime = levelMaker.listFloors[levelMaker.listFloors.Count - 1].entryTime;
    double mapTime = controller.state == States.Start ? 0d : conductor.addoffset + conductor.songposition_minusi;
    mapTime = Math.Max(0d, Math.Min(mapTime, totalTime));

    return current.WithTime(true, (int)mapTime, Math.Max(0, (int)totalTime), RDString.language);
  }

  private static float ReadCurrentProgress()
  {
    scrController controller = scrController.instance;
    scrLevelMaker levelMaker = scrLevelMaker.instance;
    if (controller == null || levelMaker == null || levelMaker.listFloors == null || levelMaker.listFloors.Count == 0)
      return 0f;

    return Mathf.Clamp01((float)controller.currentSeqID / levelMaker.listFloors.Count);
  }
}
