using HarmonyLib;

namespace Element.Infrastructure.Game.Bpm;

[HarmonyPatch(typeof(scrPlanet), "MoveToNextFloor")]
internal static class PlanetMoveToNextFloorPatch
{
  [HarmonyPostfix]
  private static void Postfix()
  {
    Main.Instance?.Runtime?.OnFloorChanged();
  }
}
