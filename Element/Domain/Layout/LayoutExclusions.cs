using System;
using System.Collections.Generic;

namespace Element.Domain.Layout;

// A rounded editor panel that native key viewers must leave uncovered.
internal readonly struct LayoutExclusion
{
  public readonly LayoutBounds Bounds;
  public readonly float Radius;

  public LayoutExclusion(LayoutBounds bounds, float radius)
  {
    Bounds = bounds;
    Radius = radius;
  }
}

internal static class LayoutExclusions
{
  /// <summary>
  /// Native viewers cut every panel with an even-odd path, where two
  /// overlapping cutouts would cancel out. Overlapping panels become one
  /// cutout covering both, keeping the smaller corner radius.
  /// </summary>
  public static void Merge(List<LayoutExclusion> exclusions)
  {
    exclusions.RemoveAll(exclusion => !(exclusion.Bounds.Width > 0 && exclusion.Bounds.Height > 0));
    bool merged = true;
    while (merged)
    {
      merged = false;
      for (int i = 0; i < exclusions.Count && !merged; i++)
      for (int j = i + 1; j < exclusions.Count && !merged; j++)
      {
        LayoutBounds a = exclusions[i].Bounds,
          b = exclusions[j].Bounds;
        if (a.X >= b.Right || b.X >= a.Right || a.Y >= b.Bottom || b.Y >= a.Bottom)
          continue;
        float left = Math.Min(a.X, b.X),
          top = Math.Min(a.Y, b.Y);
        LayoutBounds union = new(left, top, Math.Max(a.Right, b.Right) - left, Math.Max(a.Bottom, b.Bottom) - top);
        exclusions[i] = new LayoutExclusion(union, Math.Min(exclusions[i].Radius, exclusions[j].Radius));
        exclusions.RemoveAt(j);
        merged = true;
      }
    }
  }
}
