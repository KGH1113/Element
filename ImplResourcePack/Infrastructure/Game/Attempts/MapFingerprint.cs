using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using ADOFAI;
using ImplResourcePack.Domain.Attempts;

namespace ImplResourcePack.Infrastructure.Game.Attempts;

/// <summary>
/// Names a map the way JipperResourcePack does: an official level by its name, a
/// custom level by its tile angles and the events that change timing or input (speed,
/// twirl, hold, planets, pause, autoplay tiles, timing margin, multitap, kill).
/// Decorations, effects and the file name do not count, so a redecorated or
/// renamed map keeps its attempts.
/// </summary>
internal sealed class MapFingerprint : IDisposable
{
  // Reused so a long map does not leave a large buffer for the GC on every play.
  private readonly MemoryStream _buffer = new(4096);
  private readonly BinaryWriter _writer;
  private readonly MD5 _md5 = MD5.Create();

  public MapFingerprint()
  {
    _writer = new BinaryWriter(_buffer, Encoding.UTF8, true);
  }

  public bool TryCompute(out MapId map)
  {
    map = default;
    _buffer.SetLength(0);
    if (ADOBase.isOfficialLevel)
    {
      string name = ADOBase.currentLevel;
      if (string.IsNullOrEmpty(name))
        return false;
      _writer.Write((byte)0);
      _writer.Write(name);
    }
    else if (!WriteCustomLevel())
      return false;

    _writer.Flush();
    map = MapId.FromDigest(_md5.ComputeHash(_buffer.GetBuffer(), 0, (int)_buffer.Length));
    return true;
  }

  public void Dispose()
  {
    _writer.Dispose();
    _buffer.Dispose();
    _md5.Dispose();
  }

  private bool WriteCustomLevel()
  {
    scrLevelMaker levelMaker = scrLevelMaker.instance;
    scnGame game = scnGame.instance;
    if (levelMaker == null || game == null || game.levelData == null)
      return false;

    _writer.Write((byte)1);
    if (levelMaker.isOldLevel)
      _writer.Write(levelMaker.leveldata ?? string.Empty);
    else
    {
      float[] angles = levelMaker.floorAngles;
      if (angles == null)
        return false;
      _writer.Write(angles.Length);
      foreach (float angle in angles)
        _writer.Write(angle);
    }

    List<LevelEvent> events = game.events;
    if (events == null)
      return true;
    foreach (LevelEvent levelEvent in events)
      if (levelEvent != null && levelEvent.active)
        WriteEvent(levelEvent);
    return true;
  }

  private void WriteEvent(LevelEvent levelEvent)
  {
    switch (levelEvent.eventType)
    {
      case LevelEventType.SetSpeed:
        Begin(levelEvent, 0);
        object speedType = levelEvent["speedType"];
        WriteValue(speedType);
        WriteValue(
          levelEvent[speedType is SpeedType type && type == SpeedType.Bpm ? "beatsPerMinute" : "bpmMultiplier"]
        );
        break;
      case LevelEventType.Twirl:
        Begin(levelEvent, 1);
        break;
      case LevelEventType.Hold:
        Begin(levelEvent, 2);
        WriteValue(levelEvent["duration"]);
        break;
      case LevelEventType.MultiPlanet:
        Begin(levelEvent, 3);
        WriteValue(levelEvent["planets"]);
        break;
      case LevelEventType.Pause:
        Begin(levelEvent, 4);
        WriteValue(levelEvent["duration"]);
        break;
      case LevelEventType.AutoPlayTiles:
        Begin(levelEvent, 5);
        WriteValue(levelEvent["enabled"]);
        break;
      case LevelEventType.ScaleMargin:
        Begin(levelEvent, 6);
        WriteValue(levelEvent["scale"]);
        break;
      case LevelEventType.Multitap:
        Begin(levelEvent, 7);
        WriteValue(levelEvent["taps"]);
        break;
      case LevelEventType.KillPlayer:
        Begin(levelEvent, 8);
        break;
    }
  }

  private void Begin(LevelEvent levelEvent, byte kind)
  {
    _writer.Write(levelEvent.floor);
    _writer.Write(kind);
  }

  // Event values are boxed as whatever the level file held (int taps, float
  // durations, enum or bool flags), so write them by their runtime type.
  private void WriteValue(object value)
  {
    switch (value)
    {
      case null:
        _writer.Write((byte)0);
        break;
      case bool flag:
        _writer.Write((byte)1);
        _writer.Write(flag);
        break;
      case string text:
        _writer.Write((byte)2);
        _writer.Write(text);
        break;
      case Enum named:
        _writer.Write((byte)3);
        _writer.Write(Convert.ToInt64(named, CultureInfo.InvariantCulture));
        break;
      case IConvertible number:
        _writer.Write((byte)4);
        _writer.Write(number.ToDouble(CultureInfo.InvariantCulture));
        break;
      default:
        _writer.Write((byte)5);
        _writer.Write(value.ToString());
        break;
    }
  }
}
