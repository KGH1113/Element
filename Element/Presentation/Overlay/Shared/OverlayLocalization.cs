using UnityEngine;

namespace Element.Presentation.Overlay.Shared;

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

internal static class OverlayLocalization
{
  public static OverlayLabels Get(SystemLanguage language)
  {
    switch (language)
    {
      case SystemLanguage.Korean:
        return new OverlayLabels("진행도", "절대 정확도", "음악 시간", "맵 시간");
      case SystemLanguage.Japanese:
        return new OverlayLabels("進行度", "絶対精度", "音楽時間", "マップ時間");
      case SystemLanguage.ChineseSimplified:
        return new OverlayLabels("进度", "绝对精准度", "音乐时间", "谱面时间");
      case SystemLanguage.ChineseTraditional:
        return new OverlayLabels("進度", "絕對精準度", "音樂時間", "譜面時間");
      case SystemLanguage.French:
        return new OverlayLabels("Progression", "Précision absolue", "Temps musical", "Temps de la carte");
      case SystemLanguage.German:
        return new OverlayLabels("Fortschritt", "Absolute Genauigkeit", "Musikzeit", "Kartenzeit");
      case SystemLanguage.Italian:
        return new OverlayLabels("Progresso", "Precisione assoluta", "Tempo musica", "Tempo mappa");
      case SystemLanguage.Portuguese:
        return new OverlayLabels("Progresso", "Precisão absoluta", "Tempo da música", "Tempo do mapa");
      case SystemLanguage.Russian:
        return new OverlayLabels("Прогресс", "Абсолютная точность", "Время музыки", "Время карты");
      case SystemLanguage.Spanish:
        return new OverlayLabels("Progreso", "Precisión absoluta", "Tiempo de música", "Tiempo del mapa");
      default:
        return new OverlayLabels("Progress", "XAccuracy", "Music Time", "Map Time");
    }
  }
}
