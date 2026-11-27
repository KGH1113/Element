using Element.Application.Overlay;
using Element.Domain.Judgement;
using Element.Domain.Overlay.Values;

namespace Element.Presentation.Overlay.Values;

internal sealed class TimingScaleValues : IOverlayView<TimingScaleSnapshot>
{
  private readonly OverlayValues _values;

  public TimingScaleValues(OverlayValues values)
  {
    _values = values;
  }

  public void Show() { }

  public void Hide()
  {
    _values.Clear(OverlayValueId.TimingScale);
  }

  public void Render(TimingScaleSnapshot snapshot)
  {
    if (!snapshot.IsValid)
    {
      _values.Clear(OverlayValueId.TimingScale);
      return;
    }
    _values.SetNumber(OverlayValueId.TimingScale, snapshot.Scale * 100d);
  }

  public void Dispose() { }
}
