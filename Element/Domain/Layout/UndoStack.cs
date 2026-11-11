using System.Collections.Generic;

namespace Element.Domain.Layout;

/// <summary>
/// Undo and redo of whole states. A change records the state before it; a
/// change that continues the last one (the same key within the merge window)
/// joins that step instead. Past the capacity the oldest step goes.
/// </summary>
internal sealed class UndoStack<T>
{
  private readonly List<T> _undo = new();
  private readonly List<T> _redo = new();
  private readonly int _capacity;
  private readonly float _window;
  private object _lastKey;
  private float _lastAt = -1;

  /// <param name="capacity">The most undo steps kept.</param>
  /// <param name="mergeWindow">Seconds within which changes of one key make one step.</param>
  public UndoStack(int capacity, float mergeWindow = 1f)
  {
    _capacity = capacity;
    _window = mergeWindow;
  }

  public bool CanUndo => _undo.Count > 0;

  public bool CanRedo => _redo.Count > 0;

  public void Clear()
  {
    _undo.Clear();
    _redo.Clear();
    _lastKey = null;
    _lastAt = -1;
  }

  /// <summary>
  /// Whether a change of <paramref name="key"/> at <paramref name="now"/>
  /// continues the last one: the same key within the window, or any time while
  /// <paramref name="held"/>. Remembers this change either way; a null key never merges.
  /// </summary>
  public bool Continues(object key, float now, bool held = false)
  {
    bool merge = key != null && Equals(key, _lastKey) && (held || now - _lastAt < _window);
    _lastKey = key;
    _lastAt = now;
    return merge;
  }

  /// <summary>The next keyed change starts its own step.</summary>
  public void Break() => _lastKey = null;

  /// <summary>Whether a change meant to merge has a step to join.</summary>
  public bool Joins(bool merge) => merge && _undo.Count > 0;

  /// <summary>Records <paramref name="before"/> as a new step and forgets what was undone.</summary>
  public void Push(T before)
  {
    _undo.Add(before);
    if (_undo.Count > _capacity)
      _undo.RemoveAt(0);
    _redo.Clear();
  }

  /// <summary>Records <paramref name="before"/> unless the change joins the last step.</summary>
  public void Record(T before, bool merge)
  {
    if (!Joins(merge))
      Push(before);
  }

  /// <summary>Forgets what was undone.</summary>
  public void DropRedo() => _redo.Clear();

  /// <summary>The state to go back to; <paramref name="current"/> becomes the redo step.</summary>
  public bool Undo(T current, out T state) => Move(_undo, _redo, current, out state);

  /// <summary>The state undone last; <paramref name="current"/> becomes an undo step.</summary>
  public bool Redo(T current, out T state) => Move(_redo, _undo, current, out state);

  private static bool Move(List<T> from, List<T> to, T current, out T state)
  {
    if (from.Count == 0)
    {
      state = default;
      return false;
    }
    to.Add(current);
    state = from[from.Count - 1];
    from.RemoveAt(from.Count - 1);
    return true;
  }
}
