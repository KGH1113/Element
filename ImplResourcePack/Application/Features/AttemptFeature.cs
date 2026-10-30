using ImplResourcePack.Domain.Attempts;

namespace ImplResourcePack.Application.Features;

internal sealed class AttemptFeature : IOverlayFeature
{
  private readonly IAttemptSource _source;
  private readonly IOverlayView<AttemptCounts> _view;

  /// <param name="source">Null when attempt counting could not start; the view then shows zero.</param>
  public AttemptFeature(IAttemptSource source, IOverlayView<AttemptCounts> view)
  {
    _source = source;
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
    _view.Render(_source?.Read() ?? default);
  }

  public void Dispose()
  {
    _view.Dispose();
  }
}
