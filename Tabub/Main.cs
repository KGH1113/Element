using ElementKit;
using HarmonyLib;
using Tabub.Game;
using UnityModManagerNet;

namespace Tabub;

/// <summary>
/// Tabub counts the keys in each group of a chart, the way the TabubuLabubu
/// tag does, and hands the result to Element as values. It has no overlay of
/// its own: players show the values in Element's texts.
/// </summary>
public static class Main
{
  private static Harmony _harmony;
  private static bool _patched;

  public static bool Load(UnityModManager.ModEntry mod)
  {
    _harmony = new Harmony(mod.Info.Id);
    Patch();
    mod.OnToggle = (_, on) =>
    {
      if (on)
        Patch();
      else
        Unpatch();
      return true;
    };
    mod.OnUnload = _ =>
    {
      Unpatch();
      return true;
    };
    // Follows the planet tile by tile; one comparison a frame otherwise.
    mod.OnUpdate = (_, _) =>
    {
      if (_patched)
        TabubSession.Tick();
    };
    return Kit.Attach(mod);
  }

  private static void Patch()
  {
    if (_patched)
      return;
    _harmony.PatchAll(typeof(Main).Assembly);
    _patched = true;
  }

  private static void Unpatch()
  {
    if (!_patched)
      return;
    _harmony.UnpatchAll(_harmony.Id);
    _patched = false;
    TabubSession.End();
  }
}
