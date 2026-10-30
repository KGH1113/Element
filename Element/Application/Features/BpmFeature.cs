using Element.Domain.Bpm;

namespace Element.Application.Features;

internal sealed class BpmFeature : IOverlayFeature
{
  private readonly IBpmSource _reader;
  private readonly IOverlayView<BpmSnapshot> _view;

  public BpmFeature(IBpmSource reader, IOverlayView<BpmSnapshot> view)
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
    BpmSnapshot snapshot = _reader.Read();
    if (snapshot.IsValid)
      _view.Render(snapshot);
  }

  public void Dispose()
  {
    _view.Dispose();
  }
}
