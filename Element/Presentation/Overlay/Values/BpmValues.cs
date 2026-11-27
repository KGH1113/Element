using Element.Application.Overlay;
using Element.Domain.Bpm;
using Element.Domain.Overlay.Values;

namespace Element.Presentation.Overlay.Values;

internal sealed class BpmValues : IOverlayView<BpmSnapshot>
{
  private readonly OverlayValues _values;

  public BpmValues(OverlayValues values)
  {
    _values = values;
  }

  public void Show() { }

  public void Hide()
  {
    _values.Clear(OverlayValueId.TileBpm);
    _values.Clear(OverlayValueId.CurrentBpm);
    _values.Clear(OverlayValueId.KeysPerSecond);
  }

  public void Render(BpmSnapshot snapshot)
  {
    if (!snapshot.IsValid)
      return;
    // Whole, as measured: written at two decimals, and kept whole for places that read them at more or fewer.
    _values.SetNumber(OverlayValueId.TileBpm, snapshot.TileBpm);
    _values.SetNumber(OverlayValueId.CurrentBpm, snapshot.CurrentBpm);
    _values.SetNumber(OverlayValueId.KeysPerSecond, snapshot.KeysPerSecond);
  }

  public void Dispose() { }
}
