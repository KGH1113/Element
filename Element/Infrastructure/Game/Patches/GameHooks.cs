using Element.Infrastructure.Game.Editor;
using Element.Infrastructure.Game.Effects;
using Element.Infrastructure.Game.Input;
using Element.Infrastructure.Game.Settings;

namespace Element.Infrastructure.Game.Patches;

/// <summary>
/// Where the Harmony patches hand the game's calls on: the one way a patch reaches
/// the mod. Bootstrap sets each handler once its feature starts and clears them all
/// when the mod stops. A patch whose handler is missing leaves the game as it is.
/// </summary>
internal static class GameHooks
{
  public static IGameplayHooks Gameplay { get; set; }

  public static IRecordingHooks Recording { get; set; }

  public static GameHitTextHide HitTextHide { get; set; }

  public static GameKeyLimiter KeyLimiter { get; set; }

  /// <summary>Read on SkyHook's native thread too.</summary>
  public static ChatterBlocker Chatter { get; set; }

  public static EffectsController Effects { get; set; }

  public static GameplayTweaksController Tweaks { get; set; }

  public static EditorTrackpadController Trackpad { get; set; }

  public static void Clear()
  {
    Gameplay = null;
    Recording = null;
    HitTextHide = null;
    KeyLimiter = null;
    Chatter = null;
    Effects = null;
    Tweaks = null;
    Trackpad = null;
  }
}
