using Element.Tests.Infrastructure.Game;
using Element.Tests.Support;

namespace Element.Tests.Configuration.Effects;

/// <summary>
/// Effect presets and how the game plays with them, one file per part: the presets,
/// the level the game plays and the editor keeps, and preset shortcuts and zoom.
/// </summary>
internal static class EffectSettingsTests
{
  [Suite("effect presets, source preservation and stable editor filtering")]
  public static void Run()
  {
    EffectPresetTests.Run();
    EffectPlaybackTests.Run();
    EffectPresetShortcutTests.Run();
  }
}
