using System.Collections.Generic;
using Element.Configuration;

namespace Element.Domain.Layout;

/// <summary>The layout editor's undo: whole layouts, one step per finished edit, at most 64 kept.</summary>
internal sealed class LayoutHistory
{
  private readonly UndoStack<LayoutSnapshot> _steps = new(63);
  private LayoutSnapshot _current;
  public bool CanUndo => _steps.CanUndo;
  public bool CanRedo => _steps.CanRedo;

  public void Reset(IEnumerable<OverlayLayoutSettings> values)
  {
    _steps.Clear();
    _current = new LayoutSnapshot(values);
  }

  /// <summary>A step for what changed since the last one; nothing when the layout is the same.</summary>
  public void Commit(IEnumerable<OverlayLayoutSettings> values)
  {
    LayoutSnapshot next = new(values);
    if (_current == null)
    {
      _current = next;
      return;
    }
    if (_current.Matches(next))
      return;
    _steps.Push(_current);
    _current = next;
  }

  public void Undo(IList<OverlayLayoutSettings> values)
  {
    if (_steps.Undo(_current, out LayoutSnapshot state))
      (_current = state).Restore(values);
  }

  public void Redo(IList<OverlayLayoutSettings> values)
  {
    if (_steps.Redo(_current, out LayoutSnapshot state))
      (_current = state).Restore(values);
  }
}
