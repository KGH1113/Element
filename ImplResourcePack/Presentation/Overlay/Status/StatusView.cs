using System.Text;
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

  // Compare the displayed (rounded) values: raw floats change on every tile
  // even when the text would be identical.
  private readonly StringBuilder _builder = new(96);
  private long _lastStartProgress = long.MinValue;
  private long _lastProgress = long.MinValue;
  private long _lastXAccuracy = long.MinValue;
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
    double startProgress = snapshot.StartProgress * 100f;
    double progress = snapshot.Progress * 100f;
    long shownStart = NumberText.Scaled(startProgress, 0);
    long shownProgress = NumberText.Scaled(progress, 0);
    if (_lastStartProgress != shownStart || _lastProgress != shownProgress || languageChanged)
    {
      _progressText.color = OverlayTheme.ProgressColor(snapshot.Progress);
      _builder
        .Clear()
        .Append("<color=#FFFFFF>")
        .Append(labels.Progress)
        .Append(" |</color> ")
        .AppendRounded(startProgress, 0)
        .Append("%-")
        .AppendRounded(progress, 0)
        .Append('%');
      _progressText.Assign(_builder);
      _lastStartProgress = shownStart;
      _lastProgress = shownProgress;
    }

    double xAccuracy = snapshot.XAccuracy * 100f;
    long shownAccuracy = NumberText.Scaled(xAccuracy, 2);
    if (_lastXAccuracy != shownAccuracy || languageChanged)
    {
      _xAccuracyText.color = OverlayTheme.XAccuracyColor(snapshot.XAccuracy);
      _builder
        .Clear()
        .Append("<color=#FFFFFF>")
        .Append(labels.XAccuracy)
        .Append(" |</color> ")
        .AppendRounded(xAccuracy, 2)
        .Append('%');
      _xAccuracyText.Assign(_builder);
      _lastXAccuracy = shownAccuracy;
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
    _builder.Clear().Append("<color=#FFFFFF>").Append(label).Append(" |</color> ");
    AppendTime(_builder, snapshot.CurrentSeconds, snapshot.TotalSeconds).Append('~');
    AppendTime(_builder, snapshot.TotalSeconds, snapshot.TotalSeconds);
    _timeText.Assign(_builder);

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
    _fpsText.Assign(_builder.Clear().Append("<color=#FFFFFF>FPS |</color> ").AppendInt(snapshot.Fps));
    _hasLastFps = true;
    _lastFps = snapshot.Fps;
  }

  private static StringBuilder AppendTime(StringBuilder builder, int seconds, int totalSeconds)
  {
    if (totalSeconds >= 3600)
      return builder
        .AppendInt(seconds / 3600)
        .Append(':')
        .AppendTwoDigits(seconds % 3600 / 60)
        .Append(':')
        .AppendTwoDigits(seconds % 60);

    return builder.AppendInt(seconds / 60).Append(':').AppendTwoDigits(seconds % 60);
  }
}
