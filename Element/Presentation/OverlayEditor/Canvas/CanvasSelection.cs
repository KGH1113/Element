using System.Collections.Generic;
using Element.Application.Layout;

namespace Element.Presentation.OverlayEditor.Canvas;

/// <summary>
/// The selected elements. <see cref="Selected"/> is the primary one the
/// toolbars follow: setting it to an element outside the selection selects
/// that element alone, and -1 clears the selection; <see cref="Normalize"/>
/// keeps the two in step.
/// </summary>
internal sealed class CanvasSelection
{
  private readonly CanvasSession _session;
  private readonly List<IOverlayLayoutTarget> _targets = new();
  private readonly List<IOverlayLayoutTarget> _members = new();
  private int _selected = -1;

  public CanvasSelection(CanvasSession session) => _session = session;

  /// <summary>The primary element's frame index, or -1.</summary>
  public int Selected
  {
    get => _selected;
    set => _selected = value;
  }

  /// <summary>Every selected element; the primary one last after a click.</summary>
  public List<IOverlayLayoutTarget> Targets => _targets;

  public int Count => _targets.Count;

  public bool Multi => _targets.Count > 1;

  public bool HasSelection =>
    _selected >= 0
    && _selected < _session.Frames.Count
    && _session.Frames[_selected].Target.Available
    && _session.Frames[_selected].Target.Enabled;

  /// <summary>The primary element, or null.</summary>
  public IOverlayLayoutTarget Primary => HasSelection ? _session.Frames[_selected].Target : null;

  public bool Contains(IOverlayLayoutTarget target) => _targets.Contains(target);

  /// <summary>
  /// The group when the selection is one whole group (every shown element of
  /// it and nothing else); null otherwise, as when one of its elements is
  /// picked on its own.
  /// </summary>
  public string Group
  {
    get
    {
      if (_targets.Count < 2)
        return null;
      string group = _session.GroupOf(_targets[0]);
      if (group == null)
        return null;
      for (int i = 1; i < _targets.Count; i++)
        if (_session.GroupOf(_targets[i]) != group)
          return null;
      _members.Clear();
      _session.GroupFrames(group, _members);
      return _members.Count == _targets.Count ? group : null;
    }
  }

  /// <summary>Selects every shown element of <paramref name="group"/>, with <paramref name="primary"/> leading.</summary>
  public void SelectGroup(string group, int primary)
  {
    _session.GroupFrames(group, _members);
    if (_members.Count == 0)
    {
      SelectOnly(primary);
      return;
    }
    _targets.Clear();
    _targets.AddRange(_members);
    List<CanvasFrame> frames = _session.Frames;
    if (primary >= 0 && primary < frames.Count && _targets.Remove(frames[primary].Target))
      _targets.Add(frames[primary].Target);
    _selected = _session.IndexOf(_targets[_targets.Count - 1]);
  }

  /// <summary>
  /// Adds the whole group of each selected element that is in one, as a press
  /// on any of them takes its group.
  /// </summary>
  public void WidenToGroups()
  {
    for (int i = 0; i < _targets.Count; i++)
    {
      string group = _session.GroupOf(_targets[i]);
      if (group == null)
        continue;
      _session.GroupFrames(group, _members);
      foreach (IOverlayLayoutTarget member in _members)
        if (!_targets.Contains(member))
          _targets.Add(member);
    }
  }

  public static bool Selectable(IOverlayLayoutTarget target) => target.Available && target.Enabled;

  public void Clear()
  {
    _selected = -1;
    _targets.Clear();
  }

  /// <summary>Drops what can no longer be selected and follows assignments to <see cref="Selected"/>.</summary>
  public void Normalize()
  {
    List<CanvasFrame> frames = _session.Frames;
    for (int i = _targets.Count - 1; i >= 0; i--)
      if (!Selectable(_targets[i]) || _session.IndexOf(_targets[i]) < 0)
        _targets.RemoveAt(i);
    if (_selected < 0 || _selected >= frames.Count || !Selectable(frames[_selected].Target))
    {
      _selected = -1;
      _targets.Clear();
      return;
    }
    IOverlayLayoutTarget primary = frames[_selected].Target;
    if (!_targets.Contains(primary))
    {
      _targets.Clear();
      _targets.Add(primary);
    }
  }

  /// <summary>After undo: keeps whatever is still there, even when the primary is gone.</summary>
  public void Prune()
  {
    for (int i = _targets.Count - 1; i >= 0; i--)
      if (!Selectable(_targets[i]) || _session.IndexOf(_targets[i]) < 0)
        _targets.RemoveAt(i);
    // A primary that is still there keeps the selection, or was just assigned.
    if (!HasSelection)
      _selected = _targets.Count > 0 ? _session.IndexOf(_targets[_targets.Count - 1]) : -1;
    Normalize();
  }

  public void SelectOnly(int index)
  {
    List<CanvasFrame> frames = _session.Frames;
    _targets.Clear();
    _selected = index >= 0 && index < frames.Count && Selectable(frames[index].Target) ? index : -1;
    if (_selected >= 0)
      _targets.Add(frames[_selected].Target);
  }

  /// <summary>
  /// Cmd/Ctrl or Shift + click: in if it was out, out if it was in. An element
  /// in a group comes and goes with its whole group.
  /// </summary>
  public void Toggle(int index)
  {
    List<CanvasFrame> frames = _session.Frames;
    if (index < 0 || index >= frames.Count || !Selectable(frames[index].Target))
      return;
    IOverlayLayoutTarget target = frames[index].Target;
    _members.Clear();
    string group = _session.GroupOf(target);
    if (group != null)
      _session.GroupFrames(group, _members);
    if (_members.Count == 0)
      _members.Add(target);
    if (_targets.Contains(target))
    {
      foreach (IOverlayLayoutTarget member in _members)
        _targets.Remove(member);
      if (!_targets.Contains(Primary))
        _selected = _targets.Count > 0 ? _session.IndexOf(_targets[_targets.Count - 1]) : -1;
      return;
    }
    foreach (IOverlayLayoutTarget member in _members)
      if (!_targets.Contains(member) && member != target)
        _targets.Add(member);
    _targets.Add(target);
    _selected = index;
  }

  public void SelectAll()
  {
    List<CanvasFrame> frames = _session.Frames;
    _targets.Clear();
    _selected = -1;
    for (int i = 0; i < frames.Count; i++)
      if (Selectable(frames[i].Target))
      {
        _targets.Add(frames[i].Target);
        _selected = i;
      }
  }
}
