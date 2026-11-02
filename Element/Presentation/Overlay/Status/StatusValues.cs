using System.Text;
using Element.Application;
using Element.Domain.Elements;
using Element.Domain.Status;
using Element.Presentation.Overlay.Elements;
using Element.Presentation.Overlay.Shared;
using UnityEngine;

namespace Element.Presentation.Overlay.Status;

// Progress, accuracy, time and FPS, with the labels in the game's language.
internal sealed class StatusValues : IOverlayView<StatusSnapshot>
{
  private static readonly OverlayValueId[] Live =
  {
    OverlayValueId.ProgressStart,
    OverlayValueId.ProgressCurrent,
    OverlayValueId.Accuracy,
    OverlayValueId.CurrentTime,
    OverlayValueId.TotalTime,
    OverlayValueId.Fps,
  };
  private readonly OverlayValues _values;

  // Compare the displayed (rounded) values: raw floats change on every tile
  // even when the text would be identical.
  private readonly StringBuilder _builder = new(32);
  private long _lastStartProgress = long.MinValue;
  private long _lastProgress = long.MinValue;
  private long _lastXAccuracy = long.MinValue;
  private bool _hasLastLanguage;
  private SystemLanguage _lastLanguage;
  private bool _hasLastTime;
  private bool _lastWasMapTime;
  private int _lastCurrentSeconds = -1;
  private int _lastTotalSeconds = -1;

  public StatusValues(OverlayValues values)
  {
    _values = values;
  }

  public void Show() { }

  public void Hide()
  {
    foreach (OverlayValueId id in Live)
      _values.Clear(id);
    _lastStartProgress = _lastProgress = _lastXAccuracy = long.MinValue;
    _hasLastTime = false;
  }

  public void Render(StatusSnapshot snapshot)
  {
    bool languageChanged = !_hasLastLanguage || _lastLanguage != snapshot.Language;
    if (languageChanged)
    {
      OverlayLabels labels = OverlayLocalization.Get(snapshot.Language);
      _values.Set(OverlayValueId.ProgressLabel, labels.Progress);
      _values.Set(OverlayValueId.AccuracyLabel, labels.XAccuracy);
    }
    if (snapshot.HasMetrics)
      RenderMetrics(snapshot);
    if (snapshot.HasTime)
      RenderTime(snapshot, languageChanged);
    if (snapshot.HasFps)
      _values.Set(OverlayValueId.Fps, _builder.Clear().AppendInt(snapshot.Fps), snapshot.Fps);
    else
      _values.Clear(OverlayValueId.Fps);

    _hasLastLanguage = true;
    _lastLanguage = snapshot.Language;
  }

  public void Dispose() { }

  private void RenderMetrics(StatusSnapshot snapshot)
  {
    double startProgress = snapshot.StartProgress * 100f;
    double progress = snapshot.Progress * 100f;
    long shownStart = NumberText.Scaled(startProgress, 0);
    long shownProgress = NumberText.Scaled(progress, 0);
    if (_lastStartProgress != shownStart || _lastProgress != shownProgress)
    {
      _values.Set(OverlayValueId.ProgressStart, _builder.Clear().AppendRounded(startProgress, 0), shownStart);
      _values.Set(OverlayValueId.ProgressCurrent, _builder.Clear().AppendRounded(progress, 0), shownProgress);
      _lastStartProgress = shownStart;
      _lastProgress = shownProgress;
    }

    double xAccuracy = snapshot.XAccuracy * 100f;
    long shownAccuracy = NumberText.Scaled(xAccuracy, 2);
    if (_lastXAccuracy != shownAccuracy)
    {
      _values.Set(OverlayValueId.Accuracy, _builder.Clear().AppendRounded(xAccuracy, 2), shownAccuracy / 100d);
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
    _values.Set(OverlayValueId.TimeLabel, snapshot.IsMapTime ? labels.MapTime : labels.MusicTime);
    _values.Set(
      OverlayValueId.CurrentTime,
      AppendTime(_builder.Clear(), snapshot.CurrentSeconds, snapshot.TotalSeconds),
      snapshot.CurrentSeconds
    );
    _values.Set(
      OverlayValueId.TotalTime,
      AppendTime(_builder.Clear(), snapshot.TotalSeconds, snapshot.TotalSeconds),
      snapshot.TotalSeconds
    );

    _hasLastTime = true;
    _lastWasMapTime = snapshot.IsMapTime;
    _lastCurrentSeconds = snapshot.CurrentSeconds;
    _lastTotalSeconds = snapshot.TotalSeconds;
  }

  private static StringBuilder AppendTime(StringBuilder builder, int seconds, int totalSeconds) =>
    builder.AppendClock(seconds, totalSeconds);
}
