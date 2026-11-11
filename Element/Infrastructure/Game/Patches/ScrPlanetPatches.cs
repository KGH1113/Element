using HarmonyLib;

namespace Element.Infrastructure.Game.Patches;

/// <summary>
/// scrPlanet: moving to the next floor. Two postfixes at normal priority; the BPM
/// group is applied before record mode's, so the overlay's floor values update
/// first and record mode hides the error meter after.
/// </summary>
internal static class ScrPlanetPatches
{
  [HarmonyPatch(typeof(scrPlanet), "MoveToNextFloor")]
  [PatchGroup(PatchGroupNames.Bpm)]
  private static class MoveToNextFloorForOverlay
  {
    [HarmonyPostfix]
    private static void Postfix() => GameHooks.Gameplay?.OnFloorChanged();
  }

  [HarmonyPatch(typeof(scrPlanet), "MoveToNextFloor")]
  [PatchGroup(PatchGroupNames.Recording)]
  private static class MoveToNextFloorForRecording
  {
    [HarmonyPostfix]
    private static void Postfix() => GameHooks.Recording?.HideErrorMeter();
  }
}
