using HarmonyLib;

namespace Element.Infrastructure.Game.Patches;

/// <summary>scrShowIfDebug: the autoplay text, hidden in record mode.</summary>
internal static class ScrShowIfDebugPatches
{
  [HarmonyPatch(typeof(scrShowIfDebug), "Update")]
  [PatchGroup(PatchGroupNames.Recording)]
  private static class Update
  {
    [HarmonyPrefix]
    private static void Prefix(out bool __state)
    {
      __state = RDC.auto;
      if (GameHooks.Recording?.IsRecording == true)
        RDC.auto = false;
    }

    [HarmonyPostfix]
    private static void Postfix(bool __state)
    {
      RDC.auto = __state;
    }
  }
}
