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
    var remaining = new List<OverlayLayoutSettings>(destination);
    int next = 0;
    foreach (OverlayLayoutSettings value in _values)
    {
      int index = remaining.FindIndex(item => item != null && item.Id == value.Id);
      if (index < 0)
        continue;
      var target = remaining[index];
      remaining.RemoveAt(index);
      target.CopyFrom(value);
      destination[next++] = target;
    }
    // Instances created after this snapshot stay addressable for redo.
    foreach (OverlayLayoutSettings target in remaining)
    {
      if (target != null)
        target.Enabled = false;
      destination[next++] = target;
    }
  }

  public bool Matches(LayoutSnapshot other)
  {
    int next = 0;
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
      if (a.Enabled)
      {
        while (next < other._values.Count && !other._values[next].Enabled)
          next++;
        if (next == other._values.Count || other._values[next++].Id != a.Id)
          return false;
      }
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
