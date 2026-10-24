using System;
using System.Globalization;
using ImplResourcePack.Application;
using ImplResourcePack.Domain.Status;
using ImplResourcePack.Presentation.Overlay.Shared;
using TMPro;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ImplResourcePack.Presentation.Overlay.Status;

internal sealed class StatusView : IOverlayView<StatusSnapshot>
{
  private GameObject _statusRoot;
  private TextMeshProUGUI _progressText;
  private TextMeshProUGUI _xAccuracyText;
  private TextMeshProUGUI _timeText;
  private TextMeshProUGUI _fpsText;

  private float _lastStartProgress = float.NaN;
  private float _lastProgress = float.NaN;
  private float _lastXAccuracy = float.NaN;
  private bool _hasLastLanguage;
  private SystemLanguage _lastLanguage;
  private bool _hasLastTime;
  private bool _lastWasMapTime;
  private int _lastCurrentSeconds = -1;
  private int _lastTotalSeconds = -1;
  private bool _hasLastFps;
  private int _lastFps = -1;
  private int _lastThemeVersion = -1;

  public StatusView(OverlayCanvasHost host, TMP_FontAsset font, Material material)
  {
    CreateStatusText(host, font, material);
  }

  public void Show()
  {
    _statusRoot?.SetActive(true);
  }

  public void Hide()
  {
    _statusRoot?.SetActive(false);
  }

  public void Render(StatusSnapshot snapshot)
  {
    bool themeChanged = _lastThemeVersion != OverlayTheme.Version;
    bool languageChanged = !_hasLastLanguage || _lastLanguage != snapshot.Language || themeChanged;
    if (snapshot.HasMetrics)
      RenderMetrics(snapshot, languageChanged);
    if (snapshot.HasTime)
      RenderTime(snapshot, languageChanged);
    RenderFps(snapshot, themeChanged);

    _hasLastLanguage = true;
    _lastLanguage = snapshot.Language;
    _lastThemeVersion = OverlayTheme.Version;
  }

  public void Dispose()
  {
    if (_statusRoot != null)
      Object.Destroy(_statusRoot);

    _statusRoot = null;
    _progressText = null;
    _xAccuracyText = null;
    _timeText = null;
    _fpsText = null;
  }

  private void CreateStatusText(OverlayCanvasHost host, TMP_FontAsset font, Material material)
  {
    _statusRoot = OverlayTextFactory.CreateRect(
      "Status",
      host.Root.transform,
      new Vector2(0f, 1f),
      new Vector2(0f, 1f),
      new Vector2(16f, -16f),
      new Vector2(456f, 135f)
    );

    _progressText = CreateRow("Progress", -15f, font, material);
    _xAccuracyText = CreateRow("XAccuracy", -50f, font, material);
    _timeText = CreateRow("Time", -85f, font, material);
    _fpsText = CreateRow("FPS", -120f, font, material);
    host.RegisterLayout("progress", "진행도", _progressText.gameObject, _progressText, "진행도 | 0%-48%");
    host.RegisterLayout("accuracy", "정확도", _xAccuracyText.gameObject, _xAccuracyText, "정확도 | 100%");
    host.RegisterLayout("time", "음악·맵 시간", _timeText.gameObject, _timeText, "음악 시간 | 1:27~3:48");
    host.RegisterLayout("fps", "FPS", _fpsText.gameObject, _fpsText, "FPS | 120");
    _statusRoot.SetActive(false);
  }

  private TextMeshProUGUI CreateRow(string name, float y, TMP_FontAsset font, Material material)
  {
    GameObject row = OverlayTextFactory.CreateRect(
      name,
      _statusRoot.transform,
      new Vector2(0f, 1f),
      new Vector2(0.5f, 0.5f),
      new Vector2(228f, y),
      new Vector2(456f, 30f)
    );
    return OverlayTextFactory.AddText(row, font, material, 25f, TextAlignmentOptions.TopLeft);
  }

  private void RenderMetrics(StatusSnapshot snapshot, bool languageChanged)
  {
    OverlayLabels labels = OverlayLocalization.Get(snapshot.Language);
    if (_lastStartProgress != snapshot.StartProgress || _lastProgress != snapshot.Progress || languageChanged)
    {
      Color progressColor = OverlayTheme.ProgressColor(snapshot.Progress);
      _progressText.color = progressColor;
      _progressText.text =
        "<color=#FFFFFF>"
        + labels.Progress
        + " |</color> "
        + FormatWholeNumber(snapshot.StartProgress * 100f)
        + "%-"
        + FormatWholeNumber(snapshot.Progress * 100f)
        + "%";
      _lastStartProgress = snapshot.StartProgress;
      _lastProgress = snapshot.Progress;
    }

    if (_lastXAccuracy != snapshot.XAccuracy || languageChanged)
    {
      _xAccuracyText.color = OverlayTheme.XAccuracyColor(snapshot.XAccuracy);
      _xAccuracyText.text =
        "<color=#FFFFFF>" + labels.XAccuracy + " |</color> " + FormatNumber(snapshot.XAccuracy * 100f) + "%";
      _lastXAccuracy = snapshot.XAccuracy;
    }
  }

  private void RenderTime(StatusSnapshot snapshot, bool languageChanged)
  {
    if (
      _hasLastTime
      && !languageChanged
      && _lastWasMapTime == snapshot.IsMapTime
      && _lastCurrentSeconds == snapshot.CurrentSeconds
      && _lastTotalSeconds == snapshot.TotalSeconds
    )
      return;

    OverlayLabels labels = OverlayLocalization.Get(snapshot.Language);
    string label = snapshot.IsMapTime ? labels.MapTime : labels.MusicTime;
    _timeText.color = OverlayTheme.Accent;
    _timeText.text =
      "<color=#FFFFFF>"
      + label
      + " |</color> "
      + FormatTime(snapshot.CurrentSeconds, snapshot.TotalSeconds)
      + "~"
      + FormatTime(snapshot.TotalSeconds, snapshot.TotalSeconds);

    _hasLastTime = true;
    _lastWasMapTime = snapshot.IsMapTime;
    _lastCurrentSeconds = snapshot.CurrentSeconds;
    _lastTotalSeconds = snapshot.TotalSeconds;
  }

  private void RenderFps(StatusSnapshot snapshot, bool themeChanged)
  {
    if (!snapshot.HasFps)
    {
      if (_hasLastFps)
        _fpsText.text = string.Empty;

      _hasLastFps = false;
      _lastFps = -1;
      return;
    }

    if (_hasLastFps && _lastFps == snapshot.Fps && !themeChanged)
      return;

    _fpsText.color = OverlayTheme.Accent;
    _fpsText.text = "<color=#FFFFFF>FPS |</color> " + snapshot.Fps.ToString(CultureInfo.InvariantCulture);
    _hasLastFps = true;
    _lastFps = snapshot.Fps;
  }

  private static string FormatNumber(double value)
  {
    return Math.Round(value, 2).ToString(CultureInfo.InvariantCulture);
  }

  private static string FormatWholeNumber(double value)
  {
    return Math.Round(value).ToString(CultureInfo.InvariantCulture);
  }

  private static string FormatTime(int seconds, int totalSeconds)
  {
    if (totalSeconds >= 3600)
      return string.Format(
        CultureInfo.InvariantCulture,
        "{0}:{1:00}:{2:00}",
        seconds / 3600,
        seconds % 3600 / 60,
        seconds % 60
      );

    return string.Format(CultureInfo.InvariantCulture, "{0}:{1:00}", seconds / 60, seconds % 60);
  }
}
