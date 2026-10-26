using ImplResourcePack.Domain.Combo;
using ImplResourcePack.Domain.Judgement;

namespace ImplResourcePack.Application.Features;

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
    Render(false);
  }

  public void Hide()
  {
    _view.Hide();
  }

  public void RegisterHit(HitMargin hit)
  {
    bool continuesCombo = HitMarginPolicy.ContinuesCombo(hit);
    if (continuesCombo)
    {
      if (_count < int.MaxValue)
        _count++;
    }
    else
    {
      _count = 0;
    }

    Render(continuesCombo);
  }

  public void Reset()
  {
    _count = 0;
    Render(false);
  }

  public void Refresh()
  {
    Render(false);
  }

  public void Dispose()
  {
    _view.Dispose();
  }

  private void Render(bool bump)
  {
    _view.Render(new ComboSnapshot(_count, bump));
  }
}
