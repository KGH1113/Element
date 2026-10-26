using ImplResourcePack.Domain.Status;

namespace ImplResourcePack.Application.Features;

internal sealed class StatusFeature : IOverlayFeature
{
  private readonly IStatusSource _reader;
  private readonly IOverlayView<StatusSnapshot> _view;
  private StatusSnapshot _snapshot;

  public StatusFeature(IStatusSource reader, IOverlayView<StatusSnapshot> view)
  {
    _reader = reader;
    _view = view;
  }

  public void Show()
  {
    _reader.ResetRunState();
    _snapshot = default;
    _view.Show();
    RefreshAll();
  }

  public void Hide()
  {
    _view.Hide();
  }

  public void RefreshAll()
  {
    _snapshot = _reader.ReadMetrics(_snapshot);
    _snapshot = _reader.ReadTime(_snapshot);
    _view.Render(_snapshot);
  }

  public void RefreshMetrics()
  {
    StatusSnapshot next = _reader.ReadMetrics(_snapshot);
    if (
      !next.HasMetrics
      || (
        _snapshot.HasMetrics
        && _snapshot.StartProgress == next.StartProgress
        && _snapshot.Progress == next.Progress
        && _snapshot.XAccuracy == next.XAccuracy
        && _snapshot.Language == next.Language
      )
    )
      return;

    _snapshot = next;
    _view.Render(_snapshot);
  }

  public void RefreshTime(int framesPerSecond)
  {
    StatusSnapshot next = _reader.ReadTime(_snapshot, framesPerSecond);
    if (
      (!next.HasTime && !next.HasFps)
      || (
        _snapshot.HasTime == next.HasTime
        && (
          !next.HasTime
          || (
            _snapshot.IsMapTime == next.IsMapTime
            && _snapshot.CurrentSeconds == next.CurrentSeconds
            && _snapshot.TotalSeconds == next.TotalSeconds
          )
        )
        && _snapshot.HasFps == next.HasFps
        && (!next.HasFps || _snapshot.Fps == next.Fps)
        && _snapshot.Language == next.Language
      )
    )
      return;

    _snapshot = next;
    _view.Render(_snapshot);
  }

  public void Dispose()
  {
    _view.Dispose();
  }
}
