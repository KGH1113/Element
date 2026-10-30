using Element.Domain.TimingScale;

namespace Element.Application.Features;

internal sealed class TimingScaleFeature : IOverlayFeature
{
  private readonly ITimingScaleSource _reader;
  private readonly IOverlayView<TimingScaleSnapshot> _view;

  public TimingScaleFeature(ITimingScaleSource reader, IOverlayView<TimingScaleSnapshot> view)
  {
    _reader = reader;
    _view = view;
  }

  public void Show()
  {
    _view.Show();
    Refresh();
  }

  public void Hide()
  {
    _view.Hide();
  }

  public void Refresh()
  {
    _view.Render(_reader.Read());
  }

  public void Dispose()
  {
    _view.Dispose();
  }
}
