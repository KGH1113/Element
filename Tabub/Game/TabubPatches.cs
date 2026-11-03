using HarmonyLib;

namespace Tabub.Game;

// Where play starts and where the level is left; the tiles in between are
// followed each frame (TabubSession.Tick), as the tag reads CurTile.

[HarmonyPatch(typeof(scnGame), nameof(scnGame.Play))]
internal static class PlayPatch
{
  private static void Postfix(int seqID) => TabubSession.Begin(seqID);
}

// Practice and the level editor start here instead.
[HarmonyPatch(typeof(scrPressToStart), nameof(scrPressToStart.ShowText))]
internal static class PressToStartPatch
{
  private static void Postfix()
  {
    scrController controller = scrController.instance;
    TabubSession.Begin(controller != null ? controller.currentSeqID : 0);
  }
}

[HarmonyPatch(typeof(scrUIController), nameof(scrUIController.WipeToBlack))]
internal static class LeavePatch
{
  private static void Postfix() => TabubSession.End();
}

[HarmonyPatch(typeof(scrController), nameof(scrController.StartLoadingScene))]
internal static class LoadingScenePatch
{
  private static void Postfix() => TabubSession.End();
}
