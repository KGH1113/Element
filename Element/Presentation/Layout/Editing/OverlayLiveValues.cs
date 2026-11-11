using Element.Domain.Elements;
using Element.Presentation.Overlay.Elements;

namespace Element.Presentation.Layout;

/// <summary>
/// Tells an open editor when a value it shows has changed, so a level
/// playing behind it (autoplay, for one) moves its preview and numbers live.
/// Each frame costs one comparison unless something changed.
/// </summary>
internal sealed class OverlayLiveValues
{
  private readonly OverlayValues _values;
  private long _seen;

  public OverlayLiveValues(OverlayValues values)
  {
    _values = values;
    _seen = values?.Stamp ?? 0;
  }

  /// <summary>Whether a value in <paramref name="used"/> changed since the last call.</summary>
  public bool Changed(OverlayValueMask used)
  {
    if (_values == null || _values.Stamp == _seen)
      return false;
    OverlayValueMask changed = _values.ChangedSince(_seen);
    _seen = _values.Stamp;
    return changed.Overlaps(used);
  }
}
