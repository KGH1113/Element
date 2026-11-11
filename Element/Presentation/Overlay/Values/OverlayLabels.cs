namespace Element.Presentation.Overlay.Values;

internal readonly struct OverlayLabels
{
  public string Progress { get; }
  public string XAccuracy { get; }
  public string MusicTime { get; }
  public string MapTime { get; }

  public OverlayLabels(string progress, string xAccuracy, string musicTime, string mapTime)
  {
    Progress = progress;
    XAccuracy = xAccuracy;
    MusicTime = musicTime;
    MapTime = mapTime;
  }
}
