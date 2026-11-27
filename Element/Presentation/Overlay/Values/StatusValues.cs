using System.Text;
using Element.Application.Overlay;
using Element.Domain.Overlay.Values;
using Element.Domain.Status;
using UnityEngine;

namespace Element.Presentation.Overlay.Values;

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

  private readonly StringBuilder _builder = new(32);
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
    if (snapshot.HasFps && snapshot.Fps == StatusSnapshot.UnknownFps)
      _values.Set(OverlayValueId.Fps, "-");
    else if (snapshot.HasFps)
      _values.Set(OverlayValueId.Fps, _builder.Clear().AppendInt(snapshot.Fps), snapshot.Fps);
    else
      _values.Clear(OverlayValueId.Fps);

    _hasLastLanguage = true;
    _lastLanguage = snapshot.Language;
  }

  public void Dispose() { }

  // Progress and X-Accuracy go in whole, as the game has them (its float percentages); they are
  // written and cut the game's way, and change only when they do at the finest a place reads them.
  private void RenderMetrics(StatusSnapshot snapshot)
  {
    _values.SetNumber(OverlayValueId.ProgressStart, snapshot.StartProgress * 100f);
    _values.SetNumber(OverlayValueId.ProgressCurrent, snapshot.Progress * 100f);
    _values.SetNumber(OverlayValueId.Accuracy, snapshot.XAccuracy * 100f);
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
