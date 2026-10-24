using System;
using System.Collections.Generic;

namespace ImplResourcePack.Domain.Layout;

internal readonly struct LayoutSpacingGuide
{
  public readonly bool Visible;
  public readonly float Start1,
    End1,
    Start2,
    End2,
    Cross;
  private readonly float _crossMin,
    _crossMax;

  public LayoutSpacingGuide(
    float start1,
    float end1,
    float start2,
    float end2,
    float crossMin,
    float crossMax,
    float cross
  )
  {
    Visible = true;
    Start1 = start1;
    End1 = end1;
    Start2 = start2;
    End2 = end2;
    _crossMin = crossMin;
    _crossMax = crossMax;
    Cross = cross;
  }

  public LayoutSpacingGuide At(LayoutBounds bounds, bool vertical)
  {
    float min = Math.Max(_crossMin, vertical ? bounds.X : bounds.Y);
    float max = Math.Min(_crossMax, vertical ? bounds.Right : bounds.Bottom);
    return max > min
      ? new LayoutSpacingGuide(Start1, End1, Start2, End2, _crossMin, _crossMax, (min + max) * .5f)
      : default;
  }
}

// Equal edge-to-edge gaps, either between two elements or extending their row.
// No LINQ, temporary collections, or allocations on the pointer-move path.
internal static class LayoutSpacingSnap
{
  public static float Find(
    LayoutBounds moving,
    IReadOnlyList<LayoutBounds> others,
    bool vertical,
    float screenSize,
    float tolerance,
    out LayoutSpacingGuide guide
  )
  {
    guide = default;
    float position = Start(moving, vertical),
      size = Size(moving, vertical);
    float best = tolerance,
      result = position;
    for (int i = 0; i < others.Count; i++)
    for (int j = i + 1; j < others.Count; j++)
    {
      LayoutBounds a = others[i],
        b = others[j];
      if (Start(a, vertical) > Start(b, vertical))
        (a, b) = (b, a);
      float aStart = Start(a, vertical),
        aEnd = aStart + Size(a, vertical);
      float bStart = Start(b, vertical),
        bEnd = bStart + Size(b, vertical);
      float gap = bStart - aEnd;
      if (gap <= 0)
        continue;
      float crossMin = Math.Max(Start(a, !vertical), Start(b, !vertical));
      float crossMax = Math.Min(Start(a, !vertical) + Size(a, !vertical), Start(b, !vertical) + Size(b, !vertical));
      float laneMin = Math.Max(crossMin, Start(moving, !vertical));
      float laneMax = Math.Min(crossMax, Start(moving, !vertical) + Size(moving, !vertical));
      if (laneMax <= laneMin)
        continue;
      for (int placement = 0; placement < 3; placement++)
      {
        float candidate =
          placement == 0 ? aStart - gap - size
          : placement == 1 ? (aEnd + bStart - size) * .5f
          : bEnd + gap;
        float distance = Math.Abs(candidate - position);
        if (
          !OverlayLayoutGeometry.Finite(candidate)
          || distance > best
          || candidate < -size * (1 - OverlayLayoutGeometry.MinimumVisibleFraction)
          || candidate > screenSize - size * OverlayLayoutGeometry.MinimumVisibleFraction
        )
          continue;
        float s1 = placement == 0 ? candidate + size : aEnd;
        float e1 =
          placement == 0 ? aStart
          : placement == 1 ? candidate
          : bStart;
        float s2 =
          placement == 1 ? candidate + size
          : placement == 0 ? aEnd
          : bEnd;
        float e2 = placement == 2 ? candidate : bStart;
        if (e1 <= s1 || e2 <= s2)
          continue;
        bool blocked = false;
        // Only near candidates pay for occlusion checks. A third element in a
        // measured gap must not create a misleading "equal spacing" guide.
        for (int k = 0; k < others.Count; k++)
        {
          if (k == i || k == j)
            continue;
          LayoutBounds other = others[k];
          if (!Overlap(Start(other, !vertical), Start(other, !vertical) + Size(other, !vertical), laneMin, laneMax))
            continue;
          float start = Start(other, vertical),
            end = start + Size(other, vertical);
          if (
            Overlap(start, end, s1, e1)
            || Overlap(start, end, s2, e2)
            || Overlap(start, end, candidate, candidate + size)
          )
          {
            blocked = true;
            break;
          }
        }
        if (blocked || (guide.Visible && distance >= best))
          continue;
        best = distance;
        result = candidate;
        guide = new LayoutSpacingGuide(s1, e1, s2, e2, crossMin, crossMax, (laneMin + laneMax) * .5f);
      }
    }
    return result;
  }

  private static float Start(LayoutBounds b, bool vertical) => vertical ? b.Y : b.X;

  private static float Size(LayoutBounds b, bool vertical) => vertical ? b.Height : b.Width;

  private static bool Overlap(float a, float b, float c, float d) => Math.Min(b, d) > Math.Max(a, c);
}
