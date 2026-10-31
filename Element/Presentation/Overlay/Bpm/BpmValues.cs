using System.Text;
using Element.Application;
using Element.Domain.Bpm;
using Element.Domain.Elements;
using Element.Presentation.Overlay.Elements;
using Element.Presentation.Overlay.Shared;

namespace Element.Presentation.Overlay.Bpm;

internal sealed class BpmValues : IOverlayView<BpmSnapshot>
{
  private readonly OverlayValues _values;
  private readonly StringBuilder _builder = new(16);

  // Displayed values (two decimals) decide when a value changes.
  private long _lastTileBpm = long.MinValue;
  private long _lastCurrentBpm = long.MinValue;
  private long _lastKeysPerSecond = long.MinValue;

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
    _lastTileBpm = _lastCurrentBpm = _lastKeysPerSecond = long.MinValue;
  }

  public void Render(BpmSnapshot snapshot)
  {
    if (!snapshot.IsValid)
      return;
    Set(OverlayValueId.TileBpm, snapshot.TileBpm, ref _lastTileBpm);
    Set(OverlayValueId.CurrentBpm, snapshot.CurrentBpm, ref _lastCurrentBpm);
    Set(OverlayValueId.KeysPerSecond, snapshot.KeysPerSecond, ref _lastKeysPerSecond);
  }

  public void Dispose() { }

  private void Set(OverlayValueId id, double value, ref long last)
  {
    long shown = NumberText.Scaled(value, 2);
    if (shown == last)
      return;
    last = shown;
    _values.Set(id, _builder.Clear().AppendRounded(value, 2));
  }
}
