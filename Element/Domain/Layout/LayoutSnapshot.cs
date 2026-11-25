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
    RestoreOrder(destination);
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
    if (!SameOrder(other))
      return false;
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

  // The list is the drawing order: elements return to their places in it.
  // Ones made after this state keep their order after those it knows.
  private void RestoreOrder(IList<OverlayLayoutSettings> destination)
  {
    List<OverlayLayoutSettings> known = new(destination.Count);
    List<OverlayLayoutSettings> later = new();
    foreach (OverlayLayoutSettings value in _values)
    foreach (OverlayLayoutSettings target in destination)
      if (target != null && target.Id == value.Id)
      {
        known.Add(target);
        break;
      }
    foreach (OverlayLayoutSettings target in destination)
      if (!known.Contains(target))
        later.Add(target);
    int index = 0;
    foreach (OverlayLayoutSettings target in known)
      destination[index++] = target;
    foreach (OverlayLayoutSettings target in later)
      destination[index++] = target;
  }

  // The shown elements in the same drawing order.
  private bool SameOrder(LayoutSnapshot other)
  {
    int j = 0;
    foreach (OverlayLayoutSettings a in _values)
    {
      if (!a.Enabled)
        continue;
      while (j < other._values.Count && !other._values[j].Enabled)
        j++;
      if (j >= other._values.Count || other._values[j].Id != a.Id)
        return false;
      j++;
    }
    while (j < other._values.Count && !other._values[j].Enabled)
      j++;
    return j >= other._values.Count;
  }

  private OverlayLayoutSettings Find(string id)
  {
    foreach (OverlayLayoutSettings value in _values)
      if (value.Id == id)
        return value;
    return null;
  }
}
