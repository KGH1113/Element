using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using ADOFAI;
using Element.Configuration.Effects;
using Element.Infrastructure.Game.Effects;
using Element.Tests.Support;
using static Element.Tests.Support.Assert;

namespace Element.Tests.Infrastructure.Game;

internal static class EffectFilterTests
{
  [Suite("effect filter copies only changed tracks, ignores NONE tags and fixes tile animations")]
  public static void Run()
  {
    CopiesOnlyTracksThatChangeOpacity();
    IgnoresTheGamesNoneTag();
    FollowingCameraUsesTheChosenZoom();
    TrackAnimationsKeepTheOpeningAppearanceAndHidePassedTiles();
  }

  private static void TrackAnimationsKeepTheOpeningAppearanceAndHidePassedTiles()
  {
    LevelEvent settings = Fixture(
      LevelEventType.TrackSettings,
      ("trackAnimation", TrackAnimationType.Assemble),
      ("beatsAhead", 3f),
      ("trackDisappearAnimation", TrackAnimationType2.None),
      ("beatsBehind", 4f)
    );
    EffectLevelFilter.FixAnimations(settings);
    Check(
      settings["trackAnimation"] is TrackAnimationType.Assemble,
      "The map's opening tile appearance must stay, as in Enhanced Effect Remover."
    );
    Check(
      settings["trackDisappearAnimation"] is TrackAnimationType2.Fade
        && settings["beatsAhead"] is float ahead
        && ahead == 8f
        && settings["beatsBehind"] is float behind
        && behind == 0f,
      "Tiles must appear 8 beats ahead and fade out as soon as they are passed."
    );
  }

  private static void FollowingCameraUsesTheChosenZoom()
  {
    LevelEvent camera = EffectLevelFilter.FollowingCamera(Fixture(LevelEventType.CameraSettings, ("zoom", 100f)), 300);
    // The game reads it with (float)cameraSettings["zoom"]; any other boxed type throws.
    Check(camera["zoom"] is float zoom && zoom == 300f, "The camera zoom must be stored as a float percent.");
    Check(
      (float)EffectLevelFilter.FollowingCamera(Fixture(LevelEventType.CameraSettings), 4000)["zoom"] == 1000f,
      "A zoom beyond the slider must be clamped before it reaches the camera."
    );
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
}
