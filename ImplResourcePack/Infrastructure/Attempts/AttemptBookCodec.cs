using System.Collections.Generic;
using System.IO;
using ImplResourcePack.Domain.Attempts;

namespace ImplResourcePack.Infrastructure.Attempts;

/// <summary>
/// Attempts.dat: "IRPA", a format version, then each map's digest and its
/// (start tile, speed, count) entries.
/// </summary>
internal static class AttemptBookCodec
{
  private const uint Magic = 0x41505249; // "IRPA" in little-endian bytes
  private const byte Version = 1;

  public static byte[] Encode(AttemptBook book)
  {
    using MemoryStream stream = new();
    using (BinaryWriter writer = new(stream))
    {
      writer.Write(Magic);
      writer.Write(Version);
      writer.Write(book.Maps.Count);
      foreach (KeyValuePair<MapId, MapAttempts> map in book.Maps)
      {
        writer.Write(map.Key.High);
        writer.Write(map.Key.Low);
        writer.Write(map.Value.Starts.Count);
        foreach (KeyValuePair<AttemptKey, int> start in map.Value.Starts)
        {
          writer.Write(start.Key.StartFloor);
          writer.Write(start.Key.Speed);
          writer.Write(start.Value);
        }
      }
    }
    return stream.ToArray();
  }

  /// <exception cref="InvalidDataException">The data is not an attempt file this version can read.</exception>
  /// <exception cref="EndOfStreamException">The file ends early.</exception>
  public static AttemptBook Decode(byte[] data)
  {
    using BinaryReader reader = new(new MemoryStream(data, false));
    if (reader.ReadUInt32() != Magic)
      throw new InvalidDataException("This is not an attempt record file.");
    byte version = reader.ReadByte();
    if (version != Version)
      throw new InvalidDataException("Attempt record format " + version + " is not supported.");

    AttemptBook book = new();
    int maps = ReadCount(reader);
    for (int i = 0; i < maps; i++)
    {
      MapId map = new(reader.ReadUInt64(), reader.ReadUInt64());
      int starts = ReadCount(reader);
      for (int j = 0; j < starts; j++)
        book.Restore(map, new AttemptKey(reader.ReadInt32(), reader.ReadSingle()), reader.ReadInt32());
    }
    return book;
  }

  private static int ReadCount(BinaryReader reader)
  {
    int count = reader.ReadInt32();
    if (count < 0)
      throw new InvalidDataException("An attempt record entry count is negative.");
    return count;
  }
}
