using System;

namespace Element.Domain.Elements;

/// <summary>
/// A set of values by catalog id: the values a formula uses, or the values
/// that changed. Two words wide, so values other mods add fit beside the
/// built-in ones; testing a set against another stays one comparison.
/// </summary>
internal readonly struct OverlayValueMask : IEquatable<OverlayValueMask>
{
  public const int Capacity = 128;

  private readonly ulong _low;
  private readonly ulong _high;

  private OverlayValueMask(ulong low, ulong high)
  {
    _low = low;
    _high = high;
  }

  public static OverlayValueMask None => default;

  /// <summary>The set holding only <paramref name="id"/>; empty for an id outside the catalog's room.</summary>
  public static OverlayValueMask Of(OverlayValueId id)
  {
    int index = (int)id;
    if (index < 0 || index >= Capacity)
      return default;
    return index < 64 ? new OverlayValueMask(1UL << index, 0) : new OverlayValueMask(0, 1UL << (index - 64));
  }

  public bool IsEmpty => (_low | _high) == 0;

  public bool Overlaps(OverlayValueMask other) => ((_low & other._low) | (_high & other._high)) != 0;

  public bool Contains(OverlayValueId id) => Overlaps(Of(id));

  public static OverlayValueMask operator |(OverlayValueMask a, OverlayValueMask b) =>
    new(a._low | b._low, a._high | b._high);

  public static OverlayValueMask operator &(OverlayValueMask a, OverlayValueMask b) =>
    new(a._low & b._low, a._high & b._high);

  public static bool operator ==(OverlayValueMask a, OverlayValueMask b) => a.Equals(b);

  public static bool operator !=(OverlayValueMask a, OverlayValueMask b) => !a.Equals(b);

  public bool Equals(OverlayValueMask other) => _low == other._low && _high == other._high;

  public override bool Equals(object obj) => obj is OverlayValueMask other && Equals(other);

  public override int GetHashCode() => (_low ^ (_high * 31)).GetHashCode();
}
