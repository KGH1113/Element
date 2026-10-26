using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using ADOFAI;
using ImplResourcePack.Configuration.Effects;
using ImplResourcePack.Infrastructure.Game.Effects;

namespace ImplResourcePack.Tests;

internal static class EffectFilterTests
{
  public static void Run()
  {
    CopiesOnlyTracksThatChangeOpacity();
    IgnoresTheGamesNoneTag();
  }

  private static void CopiesOnlyTracksThatChangeOpacity()
  {
    Check(EffectLevelFilter.ChangesOpacity(Track(40f, false)), "An enabled partial opacity must be normalized.");
    Check(
      !EffectLevelFilter.ChangesOpacity(Track(100f, false)),
      "A full opacity already renders opaque and must not be copied."
    );
    Check(
      !EffectLevelFilter.ChangesOpacity(Track(0f, true)),
      "A disabled opacity does not change the track and must not be copied."
    );
    Check(
      !EffectLevelFilter.ChangesOpacity(Fixture(LevelEventType.MoveTrack)),
      "A track without opacity must not be copied."
    );
  }

  private static void IgnoresTheGamesNoneTag()
  {
    LevelEvent tagged = Fixture(LevelEventType.AddDecoration, ("tag", "NONE"));
    LevelEvent linked = Fixture(LevelEventType.AddDecoration, ("tag", "hit"));
    var source = new LevelData(false)
    {
      levelEvents = new EventsArray<LevelEvent>
      {
        Fixture(LevelEventType.SetConditionalEvents, ("perfectTag", "NONE"), ("lossTag", "None hit")),
      },
      decorations = new DecorationsArray<LevelEvent> { tagged, linked },
    };
    var display = new List<LevelEvent>(source.decorations);
    EffectLevelFilter.FilterEditorDecorations(display, source, RemovedEffects.Decorations);
    Check(
      display.Count == 1 && ReferenceEquals(display[0], linked),
      "The default NONE tag must not keep unrelated decorations."
    );
  }

  private static LevelEvent Track(float opacity, bool disabled)
  {
    LevelEvent track = Fixture(LevelEventType.MoveTrack, ("opacity", opacity));
    track.disabled = new Dictionary<string, bool> { ["opacity"] = disabled };
    return track;
  }

  private static LevelEvent Fixture(LevelEventType type, params (string Key, object Value)[] properties)
  {
    var result = (LevelEvent)RuntimeHelpers.GetUninitializedObject(typeof(LevelEvent));
    result.eventType = type;
    var data = new Dictionary<string, object>();
    foreach (var (key, value) in properties)
      data.Add(key, value);
    typeof(LevelEvent)
      .GetField("data", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
      .SetValue(result, data);
    return result;
  }

  private static void Check(bool value, string message)
  {
    if (!value)
      throw new InvalidOperationException(message);
  }
}
