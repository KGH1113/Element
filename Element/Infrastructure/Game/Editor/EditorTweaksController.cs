using System;
using Element.Configuration;

namespace Element.Infrastructure.Game.Editor;

/// <summary>
/// The level editor tweaks together: the selection label, fine rotation and events
/// that follow their floor. Turning floors changes the selection's stats, so both
/// ways of turning them tell the label.
/// </summary>
internal sealed class EditorTweaksController : IDisposable
{
  private readonly EditorSelectionLabel _label;
  private readonly EditorEventSync _events;
  private readonly EditorFineRotation _rotation;

  public EditorTweaksController(EditorTweaksOptions settings)
  {
    _label = new EditorSelectionLabel(settings);
    _events = new EditorEventSync(settings);
    _rotation = new EditorFineRotation(settings, _events);
  }

  public void Invalidate() => _label.Invalidate();

  public void OnFloorVisibilityChanged(scrFloor floor) => _label.OnFloorVisibilityChanged(floor);

  public void ApplySettings() => _label.ApplySettings();

  public void Tick(scnEditor editor)
  {
    if (editor == null)
      return;
    _label.FollowPlayMode(editor);
    if (_rotation.Tick(editor))
      _label.Invalidate();
    _label.Tick(editor);
  }

  internal EditorFloorMutation BeginMutation(
    scnEditor editor,
    scrFloor floor,
    string operation,
    bool clockwiseOrHorizontal
  ) => _events.Begin(editor, floor, operation, clockwiseOrHorizontal);

  internal void FinishMutation(scnEditor editor, EditorFloorMutation mutation, bool remakePath)
  {
    if (mutation == null)
      return;
    _events.Finish(editor, mutation, remakePath);
    _label.Invalidate();
  }

  public void Dispose() => _label.Dispose();
}
