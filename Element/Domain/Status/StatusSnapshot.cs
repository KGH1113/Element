using UnityEngine;

namespace Element.Domain.Status;

internal readonly struct StatusSnapshot
{
  public bool HasMetrics { get; }
  public float StartProgress { get; }
  public float Progress { get; }
  public float XAccuracy { get; }
  public bool HasTime { get; }
  public bool IsMapTime { get; }
  public int CurrentSeconds { get; }
  public int TotalSeconds { get; }
  public bool HasFps { get; }
  public int Fps { get; }
  public SystemLanguage Language { get; }

  private StatusSnapshot(
    bool hasMetrics,
    float startProgress,
    float progress,
    float xAccuracy,
    bool hasTime,
    bool isMapTime,
    int currentSeconds,
    int totalSeconds,
    bool hasFps,
    int fps,
    SystemLanguage language
  )
  {
    HasMetrics = hasMetrics;
    StartProgress = startProgress;
    Progress = progress;
    XAccuracy = xAccuracy;
    HasTime = hasTime;
    IsMapTime = isMapTime;
    CurrentSeconds = currentSeconds;
    TotalSeconds = totalSeconds;
    HasFps = hasFps;
    Fps = fps;
    Language = language;
  }

  public StatusSnapshot WithMetrics(float startProgress, float progress, float xAccuracy, SystemLanguage language)
  {
    return new StatusSnapshot(
      true,
      startProgress,
      progress,
      xAccuracy,
      HasTime,
      IsMapTime,
      CurrentSeconds,
      TotalSeconds,
      HasFps,
      Fps,
      language
    );
  }

  public StatusSnapshot WithTime(bool isMapTime, int currentSeconds, int totalSeconds, SystemLanguage language)
  {
    return new StatusSnapshot(
      HasMetrics,
      StartProgress,
      Progress,
      XAccuracy,
      true,
      isMapTime,
      currentSeconds,
      totalSeconds,
      HasFps,
      Fps,
      language
    );
  }

  public StatusSnapshot WithFps(bool hasFps, int fps)
  {
    return new StatusSnapshot(
      HasMetrics,
      StartProgress,
      Progress,
      XAccuracy,
      HasTime,
      IsMapTime,
      CurrentSeconds,
      TotalSeconds,
      hasFps,
      fps,
      Language
    );
  }
}
