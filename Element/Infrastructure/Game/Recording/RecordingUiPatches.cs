using HarmonyLib;

namespace Element.Infrastructure.Game.Recording;

internal static class RecordingUiPatchState
{
  public static bool SuppressNextFlash { get; set; }

  public static bool IsRecording => Main.Instance?.Runtime?.IsRecording ?? false;

  public static void Reapply()
  {
    Main.Instance?.Runtime?.ReapplyRecordingUi();
  }

  public static void HideErrorMeter()
  {
    Main.Instance?.Runtime?.HideRecordingErrorMeter();
  }
}

[HarmonyPatch(typeof(scrMissIndicator), "Awake")]
internal static class RecordingMissIndicatorPatch
{
  [HarmonyPostfix]
  private static void Postfix(scrMissIndicator __instance)
  {
    Main.Instance?.Runtime?.HideRecordingMissIndicator(__instance);
  }
}

[HarmonyPatch(typeof(scrShowIfDebug), "Update")]
internal static class RecordingAutoplayTextPatch
{
  [HarmonyPrefix]
  private static void Prefix(out bool __state)
  {
    __state = RDC.auto;
    if (RecordingUiPatchState.IsRecording)
      RDC.auto = false;
  }

  [HarmonyPostfix]
  private static void Postfix(bool __state)
  {
    RDC.auto = __state;
  }
}

[HarmonyPatch(typeof(scnEditor), "SwitchToEditMode")]
internal static class RecordingEditorUiPatch
{
  [HarmonyPostfix]
  private static void Postfix()
  {
    RecordingUiPatchState.Reapply();
  }
}

[HarmonyPatch(typeof(scrController), "OnLandOnPortal")]
internal static class RecordingResultUiPatch
{
  [HarmonyPrefix]
  private static void Prefix()
  {
    if (RecordingUiPatchState.IsRecording)
      RecordingUiPatchState.SuppressNextFlash = true;
  }

  [HarmonyPostfix]
  private static void Postfix()
  {
    RecordingUiPatchState.Reapply();
  }
}

[HarmonyPatch(typeof(scrFlash), "Flash")]
internal static class RecordingLastFloorFlashPatch
{
  [HarmonyPrefix]
  private static bool Prefix()
  {
    if (!RecordingUiPatchState.SuppressNextFlash)
      return true;

    RecordingUiPatchState.SuppressNextFlash = false;
    return false;
  }
}

[HarmonyPatch(typeof(scrController), "UpdateErrorMeterVisibility")]
internal static class RecordingErrorMeterVisibilityPatch
{
  [HarmonyPostfix]
  private static void Postfix()
  {
    RecordingUiPatchState.HideErrorMeter();
  }
}

[HarmonyPatch(typeof(scrController), "Awake_Rewind")]
internal static class RecordingRewindUiPatch
{
  [HarmonyPostfix]
  private static void Postfix()
  {
    RecordingUiPatchState.Reapply();
  }
}

[HarmonyPatch(typeof(scrPlanet), "MoveToNextFloor")]
internal static class RecordingFloorUiPatch
{
  [HarmonyPostfix]
  private static void Postfix()
  {
    RecordingUiPatchState.HideErrorMeter();
  }
}

[HarmonyPatch(typeof(TaroCutsceneScript), "DisplayText")]
internal static class RecordingCutsceneUiPatch
{
  [HarmonyPostfix]
  private static void Postfix()
  {
    RecordingUiPatchState.HideErrorMeter();
  }
}
