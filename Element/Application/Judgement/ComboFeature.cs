using Element.Application.Overlay;
using Element.Domain.Judgement;

namespace Element.Application.Judgement;

internal sealed class ComboFeature : IOverlayFeature
{
  private readonly IOverlayView<ComboSnapshot> _view;
  private int _count;

  public ComboFeature(IOverlayView<ComboSnapshot> view)
  {
    _view = view;
  }

  public void Show()
  {
    _count = 0;
    _view.Show();
    Render();
  }

  public void Hide()
  {
    _view.Hide();
  }

  public void RegisterHit(HitMargin hit)
  {
    if (!ComboRule.Affects(hit))
      return;

    _count = ComboRule.After(_count, hit);
    Render();
  }

  public void Reset()
  {
    _count = 0;
    Render();
  }

  public void Refresh()
  {
    Render();
  }

  public void Dispose()
  {
    _view.Dispose();
  }

  private void Render()
  {
    _view.Render(new ComboSnapshot(_count));
  }
}
