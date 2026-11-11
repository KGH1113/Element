using System.Collections.Generic;
using ADOFAI;
using ADOFAI.LevelEditor.Controls;
using HarmonyLib;

namespace Element.Infrastructure.Game.Patches;

/// <summary>PropertyControl_DecorationsList: the editor's decoration list, without the effects the preset removes.</summary>
internal static class PropertyControlDecorationsListPatches
{
  [HarmonyPatch(typeof(PropertyControl_DecorationsList), "FilterSearchResults")]
  [PatchGroup(PatchGroupNames.Effects)]
  private static class FilterSearchResults
  {
    [HarmonyPostfix]
    private static void Postfix(List<LevelEvent> ___filteredEvents) =>
      GameHooks.Effects?.FilterEditorDecorations(___filteredEvents);
  }
}
