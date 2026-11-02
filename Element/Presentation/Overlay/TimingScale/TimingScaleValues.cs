using System;
using System.Text;
using Element.Application;
using Element.Domain.Elements;
using Element.Domain.TimingScale;
using Element.Presentation.Overlay.Elements;
using Element.Presentation.Overlay.Shared;

namespace Element.Presentation.Overlay.TimingScale;

internal sealed class TimingScaleValues : IOverlayView<TimingScaleSnapshot>
{
  private readonly OverlayValues _values;
  private readonly StringBuilder _builder = new(16);
  private double _lastPercent = double.NaN;

  public TimingScaleValues(OverlayValues values)
  {
    _values = values;
  }

  public void Show() { }

  public void Hide()
  {
    _values.Clear(OverlayValueId.TimingScale);
    _lastPercent = double.NaN;
  }

  public void Render(TimingScaleSnapshot snapshot)
  {
    if (!snapshot.IsValid)
    {
      _values.Clear(OverlayValueId.TimingScale);
      _lastPercent = double.NaN;
      return;
    }

    double percent = Math.Round(snapshot.Scale * 100d, 2);
    if (_lastPercent == percent)
      return;
    _values.Set(OverlayValueId.TimingScale, _builder.Clear().AppendRounded(percent, 2), percent);
    _lastPercent = percent;
  }

  public void Dispose() { }
}
