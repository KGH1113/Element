using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Element.Infrastructure.Game.Patches;
using Element.Tests.Support;
using static Element.Tests.Support.Assert;

namespace Element.Tests.Infrastructure.Game;

/// <summary>
/// The game patches' layout: all in one folder, each in a named group, and the
/// groups ordered so same-priority patches on one game method run in the order
/// the patch files describe.
/// </summary>
internal static class PatchGroupTests
{
  private const string PatchesNamespace = "Element.Infrastructure.Game.Patches";

  [Suite("game patches: one folder, named groups, documented order")]
  public static void Run()
  {
    EveryPatchLivesInPatchesAndNamesItsGroup();
    GroupsRunInTheDocumentedOrder();
    TrackpadPatchesApplyOnlyOnMac();
  }

  private static void EveryPatchLivesInPatchesAndNamesItsGroup()
  {
    List<Type> patches = PatchClasses();
    Check(patches.Count >= 60, "every patch class is found: " + patches.Count);
    foreach (Type patch in patches)
    {
      Check(patch.Namespace == PatchesNamespace, patch.FullName + " lives in Game/Patches");
      Check(patch.DeclaringType != null, patch.FullName + " sits in its game class's patch file");
      Check(patch.GetCustomAttribute<PatchGroupAttribute>() != null, patch.FullName + " names its group");
    }
  }

  private static void GroupsRunInTheDocumentedOrder()
  {
    // SkyHookManager.HookCallback: the chatter blocker before the input gate.
    Before(PatchGroupNames.ChatterBlocker, PatchGroupNames.Input);
    // scrPlanet.MoveToNextFloor: the overlay's floor values before record mode.
    Before(PatchGroupNames.Bpm, PatchGroupNames.Recording);
    // SettingsMenu.UpdateSetting: the judgment text sync before the key limiter.
    Before(PatchGroupNames.JudgmentTextSync, PatchGroupNames.KeyLimiter);
    // scnEditor.Update: the input gate's prefix before the trackpad's.
    Before(PatchGroupNames.Input, PatchGroupNames.Trackpad);
  }

  private static void TrackpadPatchesApplyOnlyOnMac()
  {
    foreach (Type patch in PatchClasses())
    {
      if (patch.GetCustomAttribute<PatchGroupAttribute>()?.Name != PatchGroupNames.Trackpad)
        continue;
      Check(
        patch.GetMethod("Prepare", BindingFlags.Static | BindingFlags.NonPublic) != null,
        patch.FullName + " checks the platform before patching"
      );
    }
  }

  private static List<Type> PatchClasses() =>
    typeof(GameHooks)
      .Assembly.GetTypes()
      .Where(type =>
        type.GetCustomAttributesData().Any(data => data.AttributeType.FullName == "HarmonyLib.HarmonyPatch")
      )
      .ToList();

  private static void Before(string first, string second) =>
    Check(
      string.CompareOrdinal(first, second) < 0,
      first + " is applied before " + second + ", as the patch files say"
    );
}
