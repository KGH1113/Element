using HarmonyLib;

namespace Element.Infrastructure.Game.Patches;

/// <summary>scrFlash: the last floor's flash, skipped once in record mode.</summary>
internal static class ScrFlashPatches
{
  [HarmonyPatch(typeof(scrFlash), "Flash")]
  [PatchGroup(PatchGroupNames.Recording)]
  private static class Flash
  {
    [HarmonyPrefix]
    private static bool Prefix()
    {
      IRecordingHooks recording = GameHooks.Recording;
      if (recording == null || !recording.SuppressNextFlash)
        return true;

      recording.SuppressNextFlash = false;
      return false;
    }
  }
}
