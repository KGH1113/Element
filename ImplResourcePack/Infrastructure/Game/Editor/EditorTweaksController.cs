using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using ADOFAI;
using HarmonyLib;
using ImplResourcePack.Configuration;
using ImplResourcePack.Domain.Editor;
using ImplResourcePack.Presentation.Settings;
using UnityEngine;

namespace ImplResourcePack.Infrastructure.Game.Editor;

internal sealed class EditorTweaksController : IDisposable
{
  private readonly EditorTweaksOptions _settings;
  private readonly Dictionary<int, double> _selectedSpeeds = new();
  private readonly StringBuilder _text = new();
  private static readonly FieldInfo Popup = AccessTools.Field(typeof(scnEditor), "showingPopup");
  private scrFloor _labelFloor;
  private bool _dirty = true;
  private bool _wasPlaying;
  private int _selectionOptions = -1;

  public EditorTweaksController(EditorTweaksOptions settings) => _settings = settings;

  public void Invalidate() => _dirty = true;

  public void ApplySettings()
  {
    int options =
      (_settings.ShowAngle ? 1 : 0)
      | (_settings.ShowBeats ? 2 : 0)
      | (_settings.ShowCount ? 4 : 0)
      | (_settings.ShowDuration ? 8 : 0)
      | (_settings.ExcludeLastInterval ? 16 : 0);
    if (_selectionOptions == options)
      return;
    _selectionOptions = options;
    Invalidate();
  }

  public void Tick(scnEditor editor)
  {
    if (editor == null)
      return;
    if (_wasPlaying != editor.playMode)
    {
      _wasPlaying = editor.playMode;
      _dirty = true;
    }
    if (_settings.FineRotation && CanEdit(editor) && RDInput.holdingControl && RDInput.holdingAlt)
    {
      bool clockwise = UnityEngine.Input.GetKeyDown(KeyCode.Period);
      if (clockwise || UnityEngine.Input.GetKeyDown(KeyCode.Comma))
        RotateSelection(editor, clockwise);
    }
    if (!_dirty)
      return;
    _dirty = false;
    RefreshSelection(editor);
  }

  private static bool CanEdit(scnEditor editor) =>
    !UIInteractionState.BlocksPointerInput
    && !editor.playMode
    && !editor.lockPathEditing
    && !editor.userIsEditingAnInputField
    && !(Popup?.GetValue(editor) is true)
    && !editor.SelectionIsEmpty()
    && !editor.prefsContainer.gameObject.activeInHierarchy
    && !editor.particleEditorContainer.gameObject.activeInHierarchy;

  private void RotateSelection(scnEditor editor, bool clockwise)
  {
    int count = editor.selectedFloors.Count;
    int first = editor.selectedFloors[0].seqID;
    if (first <= 0)
      return;
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
        SyncEvents(editor, floor.seqID, EditorTransformKind.Rotate, delta);
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
    _dirty = true;
  }

  internal Mutation BeginMutation(scnEditor editor, scrFloor floor, string operation, bool clockwiseOrHorizontal)
  {
    if (!_settings.SyncEventPositions || editor == null || floor == null || floor.seqID <= 0 || editor.lockPathEditing)
      return null;
    EditorTransformKind kind =
      operation == "FlipFloor"
        ? (clockwiseOrHorizontal ? EditorTransformKind.FlipHorizontal : EditorTransformKind.FlipVertical)
        : EditorTransformKind.Rotate;
    float degrees = operation == "RotateFloor180" ? 180 : (clockwiseOrHorizontal ? -90 : 90);
    return new Mutation(new SaveStateScope(editor, clearRedo: true), floor.seqID, kind, degrees);
  }

  internal void FinishMutation(scnEditor editor, Mutation mutation, bool remakePath)
  {
    if (mutation == null)
      return;
    bool changed = SyncEvents(editor, mutation.Floor, mutation.Kind, mutation.Degrees);
    if (changed && remakePath)
      editor.ApplyEventsToFloors();
    _dirty = true;
  }

  private bool SyncEvents(scnEditor editor, int floor, EditorTransformKind kind, float degrees)
  {
    if (!_settings.SyncEventPositions)
      return false;
    bool changed = false;
    foreach (LevelEvent levelEvent in editor.events)
    {
      if (levelEvent.floor != floor)
        continue;
      string key = levelEvent.eventType switch
      {
        LevelEventType.MoveCamera => "position",
        LevelEventType.MoveTrack or LevelEventType.PositionTrack => "positionOffset",
        _ => null,
      };
      if (key == null || !levelEvent.TryGet(key, out Vector2 offset))
        continue;
      (float x, float y) = EditorTransform.Position(offset.x, offset.y, kind, degrees);
      levelEvent[key] = new Vector2(x, y);
      changed = true;
    }
    return changed;
  }

  private void RefreshSelection(scnEditor editor)
  {
    RestoreLabel(editor);
    if (!_settings.HasSelectionInfo || editor.playMode || editor.showFloorNums || editor.SelectionIsEmpty())
      return;
    scrFloor anchor = null;
    float nearest = float.PositiveInfinity;
    Vector3 cameraPosition = editor.camera != null ? editor.camera.transform.position : Vector3.zero;
    foreach (scrFloor floor in editor.selectedFloors)
    {
      if (floor == null || !floor.enabled || floor.editorNumText == null)
        continue;
      Vector3 delta = floor.transform.position - cameraPosition;
      float distance = delta.x * delta.x + delta.y * delta.y;
      if (distance >= nearest)
        continue;
      nearest = distance;
      anchor = floor;
    }
    if (anchor == null)
      return;

    double angle = 0;
    int count = editor.selectedFloors.Count;
    int intervals = count - (_settings.ExcludeLastInterval && count > 1 ? 1 : 0);
    double startSpeed = editor.selectedFloors[0].speed;
    _selectedSpeeds.Clear();
    if (_settings.ShowAngle || _settings.ShowBeats || _settings.ShowDuration)
    {
      ADOBase.lm?.CalculateFloorAngleLengths();
      for (int i = 0; i < intervals; i++)
      {
        scrFloor floor = editor.selectedFloors[i];
        if (floor.seqID >= editor.floors.Count - 1 || floor.speed <= 0)
          continue;
        double ratio = startSpeed / floor.speed;
        angle += floor.angleLength * 180 / Math.PI * ratio;
        _selectedSpeeds[floor.seqID] = ratio;
      }
      foreach (LevelEvent levelEvent in editor.events)
      {
        if (!levelEvent.active || !_selectedSpeeds.TryGetValue(levelEvent.floor, out double ratio))
          continue;
        if (levelEvent.eventType == LevelEventType.Pause)
          angle += levelEvent.GetFloat("duration") * 180 * ratio;
        else if (levelEvent.eventType == LevelEventType.FreeRoam)
          angle += levelEvent.GetInt("duration") * 180 * ratio;
      }
    }
    _text.Clear();
    if (_settings.ShowAngle)
      Append(angle, "0.####", "°", "FF9980");
    if (_settings.ShowBeats)
      Append(angle / 180, "0.####", " beats", "93C8F4");
    if (_settings.ShowCount)
      Append(count, "0", " tiles", "C5C5C5");
    if (_settings.ShowDuration)
    {
      double denominator = startSpeed * editor.levelData.bpm * 3;
      Append(denominator > 0 ? angle / denominator : 0, "0.######", " s", "FFFFFF");
    }
    anchor.editorNumText.letterText.text = _text.ToString();
    anchor.editorNumText.gameObject.SetActive(true);
    _labelFloor = anchor;
  }

  private void Append(double value, string format, string suffix, string color)
  {
    if (_text.Length > 0)
      _text.Append('\n');
    _text.Append("<color=#").Append(color).Append('>');
    _text.Append(value.ToString(format, CultureInfo.InvariantCulture)).Append(suffix).Append("</color>");
  }

  private void RestoreLabel(scnEditor editor)
  {
    if (_labelFloor != null && _labelFloor.editorNumText != null)
    {
      _labelFloor.editorNumText.letterText.text = _labelFloor.seqID.ToString(CultureInfo.InvariantCulture);
      _labelFloor.editorNumText.gameObject.SetActive(editor != null && editor.showFloorNums && !editor.playMode);
    }
    _labelFloor = null;
  }

  public void Dispose() => RestoreLabel(ADOBase.editor);

  internal sealed class Mutation : IDisposable
  {
    private SaveStateScope _scope;
    internal readonly int Floor;
    internal readonly EditorTransformKind Kind;
    internal readonly float Degrees;

    internal Mutation(SaveStateScope scope, int floor, EditorTransformKind kind, float degrees)
    {
      _scope = scope;
      Floor = floor;
      Kind = kind;
      Degrees = degrees;
    }

    public void Dispose()
    {
      _scope?.Dispose();
      _scope = null;
    }
  }
}
