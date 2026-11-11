using System.Reflection;
using Element.Application.Input;
using Element.Configuration;
using Element.Domain.Editor;
using HarmonyLib;
using UnityEngine;

namespace Element.Infrastructure.Game.Editor;

/// <summary>
/// Ctrl+Alt+Comma and Ctrl+Alt+Period turn the selected floors by the chosen step,
/// as one undo step; their events turn with them.
/// </summary>
internal sealed class EditorFineRotation
{
  private static readonly FieldInfo Popup = AccessTools.Field(typeof(scnEditor), "showingPopup");

  private readonly EditorTweaksOptions _settings;
  private readonly EditorEventSync _events;

  public EditorFineRotation(EditorTweaksOptions settings, EditorEventSync events)
  {
    _settings = settings;
    _events = events;
  }

  /// <summary>Every editor frame; true when the selection turned.</summary>
  public bool Tick(scnEditor editor)
  {
    if (!_settings.FineRotation || !CanEdit(editor) || !RDInput.holdingControl || !RDInput.holdingAlt)
      return false;
    bool clockwise = UnityEngine.Input.GetKeyDown(KeyCode.Period);
    return (clockwise || UnityEngine.Input.GetKeyDown(KeyCode.Comma)) && Rotate(editor, clockwise);
  }

  private static bool CanEdit(scnEditor editor) =>
    !InputGate.BlocksPointerInput
    && !editor.playMode
    && !editor.lockPathEditing
    && !editor.userIsEditingAnInputField
    && !(Popup?.GetValue(editor) is true)
    && !editor.SelectionIsEmpty()
    && !editor.prefsContainer.gameObject.activeInHierarchy
    && !editor.particleEditorContainer.gameObject.activeInHierarchy;

  private bool Rotate(scnEditor editor, bool clockwise)
  {
    int count = editor.selectedFloors.Count;
    int first = editor.selectedFloors[0].seqID;
    if (first <= 0)
      return false;
    int last = editor.selectedFloors[count - 1].seqID;
    int selectionAnchor = editor.multiSelectPoint != null ? editor.multiSelectPoint.seqID : first;
    float delta = (clockwise ? -1 : 1) * (editor.levelData.isOldLevel ? 15 : _settings.RotationStep);
    using (new SaveStateScope(editor, clearRedo: true))
    {
      char[] path = editor.levelData.isOldLevel ? editor.levelData.pathData.ToCharArray() : null;
      foreach (scrFloor floor in editor.selectedFloors)
      {
        int index = floor.seqID - 1;
        if (path != null)
        {
          if (index >= 0 && index < path.Length)
            path[index] = EditorTransform.RotateLegacy(path[index], clockwise);
        }
        else if (index >= 0 && index < editor.levelData.angleData.Count)
          editor.levelData.angleData[index] = EditorTransform.RotateAngle(editor.levelData.angleData[index], delta);
        _events.Apply(editor, floor.seqID, EditorTransformKind.Rotate, delta);
      }
      if (path != null)
        editor.levelData.pathData = new string(path);
      editor.RemakePath();
      if (first == last)
        editor.SelectFloor(editor.floors[first]);
      else
        editor.MultiSelectFloors(editor.floors[first], editor.floors[last]);
      editor.multiSelectPoint = editor.floors[Mathf.Clamp(selectionAnchor, 0, editor.floors.Count - 1)];
    }
    return true;
  }
}
