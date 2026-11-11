using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using ADOFAI;
using Element.Configuration;
using UnityEngine;

namespace Element.Infrastructure.Game.Editor;

/// <summary>
/// The selection's angle, beats, tile count and duration, written over the floor
/// number of the selected floor nearest the camera. The game's own number comes
/// back when the selection, the setting or the editor's mode no longer shows it.
/// </summary>
internal sealed class EditorSelectionLabel : IDisposable
{
  private readonly EditorTweaksOptions _settings;
  private readonly Dictionary<int, double> _selectedSpeeds = new();
  private readonly StringBuilder _text = new();
  private scrFloor _labelFloor;
  private string _labelText;

  // Selection stats change only with the selection or level; placement follows
  // the camera. Visibility callbacks fire for every floor crossing the view, so
  // they only request a throttled placement update.
  private bool _statsDirty = true;
  private bool _placementDirty;
  private bool _labelOverwritten;
  private float _nextPlacement;
  private bool _wasPlaying;
  private int _selectionOptions = -1;

  public EditorSelectionLabel(EditorTweaksOptions settings) => _settings = settings;

  /// <summary>The selection or the level changed: the stats are read again on the next frame.</summary>
  public void Invalidate() => _statsDirty = true;

  public void OnFloorVisibilityChanged(scrFloor floor)
  {
    if (floor == null || _labelText == null)
      return;
    // The game rewrites a floor's number text when it becomes visible.
    if (floor == _labelFloor)
    {
      _labelOverwritten = true;
      return;
    }
    scnEditor editor = scnEditor.instance;
    if (editor == null || editor.SelectionIsEmpty())
      return;
    // Selections are contiguous, so a seqID range check finds members in O(1).
    int first = editor.selectedFloors[0].seqID;
    int last = editor.selectedFloors[editor.selectedFloors.Count - 1].seqID;
    if (floor.seqID >= Math.Min(first, last) && floor.seqID <= Math.Max(first, last))
      _placementDirty = true;
  }

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

  /// <summary>Every editor frame, after the other editor tweaks.</summary>
  public void Tick(scnEditor editor)
  {
    if (_statsDirty)
    {
      _statsDirty = _placementDirty = _labelOverwritten = false;
      RefreshSelection(editor);
      return;
    }
    if (_labelOverwritten)
    {
      _labelOverwritten = false;
      ShowLabel(_labelFloor);
    }
    if (_placementDirty && Time.unscaledTime >= _nextPlacement)
    {
      _placementDirty = false;
      _nextPlacement = Time.unscaledTime + .1f;
      PlaceLabel(editor);
    }
  }

  /// <summary>Entering or leaving play mode hides or shows the stats.</summary>
  public void FollowPlayMode(scnEditor editor)
  {
    if (_wasPlaying == editor.playMode)
      return;
    _wasPlaying = editor.playMode;
    _statsDirty = true;
  }

  private void RefreshSelection(scnEditor editor)
  {
    _labelText = null;
    if (!_settings.HasSelectionInfo || editor.playMode || editor.showFloorNums || editor.SelectionIsEmpty())
    {
      RestoreLabel(editor);
      return;
    }
    scrFloor anchor = NearestSelected(editor);
    if (anchor == null)
    {
      RestoreLabel(editor);
      return;
    }

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
    _labelText = _text.ToString();
    if (anchor != _labelFloor)
      RestoreLabel(editor);
    _labelFloor = anchor;
    ShowLabel(anchor);
  }

  private void PlaceLabel(scnEditor editor)
  {
    if (_labelText == null)
      return;
    scrFloor anchor = NearestSelected(editor);
    if (anchor == null || anchor == _labelFloor)
      return;
    RestoreLabel(editor);
    _labelFloor = anchor;
    ShowLabel(anchor);
  }

  private void ShowLabel(scrFloor floor)
  {
    if (floor == null || floor.editorNumText == null || _labelText == null)
      return;
    if (floor.editorNumText.letterText.text != _labelText)
      floor.editorNumText.letterText.text = _labelText;
    floor.editorNumText.gameObject.SetActive(true);
  }

  private static scrFloor NearestSelected(scnEditor editor)
  {
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
    return anchor;
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

  public void Dispose()
  {
    _labelText = null;
    RestoreLabel(ADOBase.editor);
  }
}
