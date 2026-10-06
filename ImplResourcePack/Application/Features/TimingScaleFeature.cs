using ImplResourcePack.Domain.TimingScale;
using ImplResourcePack.Infrastructure.Game.TimingScale;

namespace ImplResourcePack.Application.Features;

internal sealed class TimingScaleFeature : IOverlayFeature
{
  private readonly TimingScaleReader _reader;
  private readonly IOverlayView<TimingScaleSnapshot> _view;

  public TimingScaleFeature(TimingScaleReader reader, IOverlayView<TimingScaleSnapshot> view)
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
