using System;

namespace ImplResourcePack.Domain.Attempts;

/// <summary>Names a map by a 128-bit digest of what decides how it plays.</summary>
internal readonly struct MapId : IEquatable<MapId>
{
  public MapId(ulong high, ulong low)
  {
    High = high;
    Low = low;
  }

  public ulong High { get; }
  public ulong Low { get; }

  public static MapId FromDigest(byte[] digest)
  {
    if (digest == null || digest.Length < 16)
      throw new ArgumentException("A map digest needs 16 bytes.", nameof(digest));
    return new MapId(BitConverter.ToUInt64(digest, 0), BitConverter.ToUInt64(digest, 8));
  }

  public bool Equals(MapId other) => High == other.High && Low == other.Low;

  public override bool Equals(object obj) => obj is MapId other && Equals(other);

  public override int GetHashCode() => (High ^ Low).GetHashCode();
}
