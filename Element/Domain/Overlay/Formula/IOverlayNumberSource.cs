using Element.Domain.Overlay.Values;

namespace Element.Domain.Overlay.Formula;

/// <summary>Numbers for values in a formula; false when the game has none right now.</summary>
internal interface IOverlayNumberSource
{
  bool TryGetNumber(OverlayValueId id, out double number);
}
