using ElementKit;
using HarmonyLib;
using Tabub.Game;
using UnityModManagerNet;

namespace Tabub;

/// <summary>
/// Tabub works out which hand plays each stretch of a chart and how many keys
/// it presses together, and hands that to Element as values. It has no
/// overlay of its own: players show the values in Element's texts and shapes.
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
