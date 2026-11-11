using System.Collections.Generic;
using Element.Application.Layout;

namespace Element.Presentation.Layout.Canvas;

/// <summary>
/// The selected elements. <see cref="Selected"/> is the primary one the
/// toolbars follow: setting it to an element outside the selection selects
/// that element alone, and -1 clears the selection; <see cref="Normalize"/>
/// keeps the two in step.
/// </summary>
internal sealed class LayoutSelection
{
  private readonly LayoutSession _session;
  private readonly List<IOverlayLayoutTarget> _targets = new();
  private int _selected = -1;

  public LayoutSelection(LayoutSession session) => _session = session;

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

  public static bool Selectable(IOverlayLayoutTarget target) => target.Available && target.Enabled;

  public void Clear()
  {
    _selected = -1;
    _targets.Clear();
  }

  /// <summary>Drops what can no longer be selected and follows assignments to <see cref="Selected"/>.</summary>
  public void Normalize()
  {
    List<LayoutFrame> frames = _session.Frames;
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
    List<LayoutFrame> frames = _session.Frames;
    _targets.Clear();
    _selected = index >= 0 && index < frames.Count && Selectable(frames[index].Target) ? index : -1;
    if (_selected >= 0)
      _targets.Add(frames[_selected].Target);
  }

  /// <summary>Cmd/Ctrl or Shift + click: in if it was out, out if it was in.</summary>
  public void Toggle(int index)
  {
    List<LayoutFrame> frames = _session.Frames;
    if (index < 0 || index >= frames.Count || !Selectable(frames[index].Target))
      return;
    IOverlayLayoutTarget target = frames[index].Target;
    if (_targets.Remove(target))
    {
      if (_selected == index)
        _selected = _targets.Count > 0 ? _session.IndexOf(_targets[_targets.Count - 1]) : -1;
      return;
    }
    _targets.Add(target);
    _selected = index;
  }

  public void SelectAll()
  {
    List<LayoutFrame> frames = _session.Frames;
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
