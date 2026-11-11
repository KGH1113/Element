using ADOFAI;
using Element.Configuration;
using Element.Domain.Editor;
using UnityEngine;

namespace Element.Infrastructure.Game.Editor;

/// <summary>
/// Moves a floor's Move Camera, Move Track and Position Track offsets with it when
/// the floor is flipped or rotated, so they keep pointing the same way.
/// </summary>
internal sealed class EditorEventSync
{
  private readonly EditorTweaksOptions _settings;

  public EditorEventSync(EditorTweaksOptions settings) => _settings = settings;

  /// <summary>Before the editor flips or rotates <paramref name="floor"/>; null when its events stay.</summary>
  internal EditorFloorMutation Begin(scnEditor editor, scrFloor floor, string operation, bool clockwiseOrHorizontal)
  {
    if (!_settings.SyncEventPositions || editor == null || floor == null || floor.seqID <= 0 || editor.lockPathEditing)
      return null;
    EditorTransformKind kind =
      operation == "FlipFloor"
        ? (clockwiseOrHorizontal ? EditorTransformKind.FlipHorizontal : EditorTransformKind.FlipVertical)
        : EditorTransformKind.Rotate;
    float degrees = operation == "RotateFloor180" ? 180 : (clockwiseOrHorizontal ? -90 : 90);
    return new EditorFloorMutation(new SaveStateScope(editor, clearRedo: true), floor.seqID, kind, degrees);
  }

  /// <summary>The flip or rotation is done: its floor's events follow.</summary>
  internal void Finish(scnEditor editor, EditorFloorMutation mutation, bool remakePath)
  {
    bool changed = Apply(editor, mutation.Floor, mutation.Kind, mutation.Degrees);
    if (changed && remakePath)
      editor.ApplyEventsToFloors();
  }

  /// <summary>Turns the offsets of <paramref name="floor"/>'s events; true when one changed.</summary>
  internal bool Apply(scnEditor editor, int floor, EditorTransformKind kind, float degrees)
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
}
