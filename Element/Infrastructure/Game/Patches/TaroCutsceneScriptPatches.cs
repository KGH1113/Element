using HarmonyLib;

namespace Element.Infrastructure.Game.Patches;

/// <summary>TaroCutsceneScript: cutscene text, which shows the error meter again.</summary>
internal static class TaroCutsceneScriptPatches
{
  [HarmonyPatch(typeof(TaroCutsceneScript), "DisplayText")]
  [PatchGroup(PatchGroupNames.Recording)]
  private static class DisplayText
  {
    [HarmonyPostfix]
    private static void Postfix() => GameHooks.Recording?.HideErrorMeter();
  }
}
