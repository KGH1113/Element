using System;
using Element.Domain.Layout;
using UnityEngine;

namespace Element.Presentation.Layout.ContentEditors.Shell;

/// <summary>
/// An editor's own undo while it is open: the whole edited state is taken
/// before each change and put back on undo and redo. Changes of one number
/// within a second make one step.
/// </summary>
internal sealed class OverlayEditorHistory<T>
{
  private readonly UndoStack<T> _stack = new(100);
  private readonly Func<T> _capture;
  private readonly Action<T> _restore;

  /// <param name="capture">A copy of the state as it is now.</param>
  /// <param name="restore">Puts a copy taken earlier back.</param>
  public OverlayEditorHistory(Func<T> capture, Action<T> restore)
  {
    _capture = capture;
    _restore = restore;
  }

  public void Clear() => _stack.Clear();

  /// <summary>Takes the state before a change, unless it <paramref name="merge"/>s into the last step.</summary>
  public void Remember(bool merge)
  {
    if (!_stack.Joins(merge))
      _stack.Push(_capture());
  }

  /// <summary>
  /// Takes the state before a change of <paramref name="key"/>; a change of
  /// the same key within a second joins the last step. A null key never joins.
  /// </summary>
  public void Remember(string key) => Remember(Continues(key));

  /// <summary>Whether a change of <paramref name="key"/> now continues the last one; remembers it either way.</summary>
  public bool Continues(string key) => _stack.Continues(key, Time.unscaledTime);

  /// <summary>The next change of any key starts its own step.</summary>
  public void Break() => _stack.Break();

  public void Undo()
  {
    if (_stack.CanUndo && _stack.Undo(_capture(), out T state))
      _restore(state);
  }

  public void Redo()
  {
    if (_stack.CanRedo && _stack.Redo(_capture(), out T state))
      _restore(state);
  }
}
