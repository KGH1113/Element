using Element.Application.Input;
using Element.Infrastructure.Game.Input;
using HarmonyLib;
using SkyHook;

namespace Element.Infrastructure.Game.Patches;

/// <summary>
/// SkyHookManager: key events from the native keyboard hook, on its own thread. No
/// Unity calls or allocation here, and nothing may escape into native code.
/// HookCallback has two first-priority prefixes. The chatter blocker's group is applied
/// before the input gate's, so it runs first and records every press; when it drops
/// one, the input gate's prefix is skipped, as Harmony skips prefixes that return bool
/// once one returned false.
/// </summary>
internal static class SkyHookManagerPatches
{
  [HarmonyPatch(typeof(SkyHookManager), "HookCallback")]
  [PatchGroup(PatchGroupNames.ChatterBlocker)]
  private static class HookCallbackForChatterBlocker
  {
    [HarmonyPrefix]
    [HarmonyPriority(Priority.First)]
    private static bool Prefix(SkyHookEvent ev)
    {
      ChatterBlocker chatter = GameHooks.Chatter;
      return chatter == null || chatter.AcceptAsync(ev);
    }
  }

  [HarmonyPatch(typeof(SkyHookManager), "HookCallback")]
  [PatchGroup(PatchGroupNames.Input)]
  private static class HookCallbackForInputGate
  {
    [HarmonyPrefix]
    [HarmonyPriority(Priority.First)]
    private static bool Prefix(SkyHookEvent ev)
    {
      // Releases must reach the game's hook state even while a modal is open.
      return ev.Type != SkyHook.EventType.KeyPressed || !InputGate.BlocksAsyncKeyboard;
    }
  }
}
