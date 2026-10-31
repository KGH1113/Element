using System.Collections.Generic;
using Element.Configuration;

namespace Element.Domain.Layout;

// Store placement intent, not corrected display pixels or Unity object references.
internal sealed class LayoutSnapshot
{
  private readonly List<OverlayLayoutSettings> _values = new();

  public LayoutSnapshot(IEnumerable<OverlayLayoutSettings> source)
  {
    foreach (OverlayLayoutSettings value in source)
      if (value != null)
        _values.Add(
          new OverlayLayoutSettings
          {
            Id = value.Id,
            TemplateId = value.TemplateId,
            Enabled = value.Enabled,
            Customized = value.Customized,
            CenterX = value.CenterX,
            CenterY = value.CenterY,
            Scale = value.Scale,
            LayoutVersion = value.LayoutVersion,
            Anchor = value.Anchor,
            OffsetX = value.OffsetX,
            OffsetY = value.OffsetY,
          }
        );
  }

  public void Restore(IList<OverlayLayoutSettings> destination)
  {
    foreach (OverlayLayoutSettings target in destination)
    {
      if (target == null)
        continue;
      OverlayLayoutSettings value = Find(target.Id);
      if (value == null)
      {
        // Newly created instances remain addressable for redo, but disappear
        // when restoring a state that predates their creation.
        target.Enabled = false;
        continue;
      }
      target.TemplateId = value.TemplateId;
      target.Enabled = value.Enabled;
      target.Customized = value.Customized;
      target.CenterX = value.CenterX;
      target.CenterY = value.CenterY;
      target.Scale = value.Scale;
      target.LayoutVersion = value.LayoutVersion;
      target.Anchor = value.Anchor;
      target.OffsetX = value.OffsetX;
      target.OffsetY = value.OffsetY;
    }
  }

  public bool Matches(LayoutSnapshot other)
  {
    foreach (OverlayLayoutSettings a in _values)
    {
      OverlayLayoutSettings b = other.Find(a.Id);
      if (b == null)
      {
        if (a.Enabled)
          return false;
        continue;
      }
      if (
        a.TemplateId != b.TemplateId
        || a.Enabled != b.Enabled
        || a.Customized != b.Customized
        || a.CenterX != b.CenterX
        || a.CenterY != b.CenterY
        || a.Scale != b.Scale
        || a.LayoutVersion != b.LayoutVersion
        || a.Anchor != b.Anchor
        || a.OffsetX != b.OffsetX
        || a.OffsetY != b.OffsetY
      )
        return false;
    }
    foreach (OverlayLayoutSettings b in other._values)
      if (b.Enabled && Find(b.Id) == null)
        return false;
    return true;
  }

  private OverlayLayoutSettings Find(string id)
  {
    foreach (OverlayLayoutSettings value in _values)
      if (value.Id == id)
        return value;
    return null;
  }
}

internal sealed class LayoutHistory
{
  private readonly List<LayoutSnapshot> _states = new();
  private int _index;
  public bool CanUndo => _index > 0;
  public bool CanRedo => _index + 1 < _states.Count;

  public void Reset(IEnumerable<OverlayLayoutSettings> values)
  {
    _states.Clear();
    _states.Add(new LayoutSnapshot(values));
    _index = 0;
  }

  public void Commit(IEnumerable<OverlayLayoutSettings> values)
  {
    LayoutSnapshot next = new(values);
    if (_states.Count == 0)
    {
      _states.Add(next);
      _index = 0;
      return;
    }
    if (_states[_index].Matches(next))
      return;
    if (CanRedo)
      _states.RemoveRange(_index + 1, _states.Count - _index - 1);
    _states.Add(next);
    if (_states.Count > 64)
      _states.RemoveAt(0);
    _index = _states.Count - 1;
  }

  public void Undo(IList<OverlayLayoutSettings> values)
  {
    if (CanUndo)
      _states[--_index].Restore(values);
  }

  public void Redo(IList<OverlayLayoutSettings> values)
  {
    if (CanRedo)
      _states[++_index].Restore(values);
  }
}
