using Element.Application.Input;
using HarmonyLib;

namespace Element.Infrastructure.Game.Patches;

/// <summary>RDInputType_Mouse: the mouse as a game input, held back while the input gate holds the pointer.</summary>
internal static class RDInputTypeMousePatches
{
  [HarmonyPatch(typeof(RDInputType_Mouse), nameof(RDInputType_Mouse.Main))]
  [PatchGroup(PatchGroupNames.Input)]
  private static class MainPresses
  {
    [HarmonyPrefix]
    private static bool Prefix(ref int __result)
    {
      if (!InputGate.BlocksPointerInput)
        return true;

      __result = 0;
      return false;
    }
  }
}
