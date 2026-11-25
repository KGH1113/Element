using System.Collections.Generic;
using Element.Configuration.Overlay;

namespace Element.Domain.Layout;

// Store placement and content intent, not corrected display pixels or Unity object references.
internal sealed class LayoutSnapshot
{
  private readonly List<OverlayLayoutSettings> _values = new();

  public LayoutSnapshot(IEnumerable<OverlayLayoutSettings> source)
  {
    foreach (OverlayLayoutSettings value in source)
      if (value != null)
        _values.Add(value.Clone());
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
      target.CopyFrom(value);
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
      if (!a.SameAs(b))
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
