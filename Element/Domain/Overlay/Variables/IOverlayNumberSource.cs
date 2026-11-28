using Element.Domain.Overlay.Values;

namespace Element.Domain.Overlay.Variables;

/// <summary>
/// Numbers for values in a formula, whole: each place cuts a value with
/// decimals to as many as it reads (<see cref="OverlayValueNumbers"/>).
/// False when the game has none right now.
/// </summary>
internal interface IOverlayNumberSource
{
  bool TryGetNumber(OverlayValueId id, out double number);
}
