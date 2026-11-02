using System;
using System.Collections.Generic;

namespace Element.Domain.Layout;

internal enum LayoutAlignment
{
  Left,
  Center,
  Right,
  Top,
  Middle,
  Bottom,
}

/// <summary>
/// Lines several boxes up with each other, or spaces them evenly, within the
/// box they cover together, as a design tool's align and distribute do.
/// </summary>
internal static class LayoutArrange
{
  public static LayoutBounds Union(IReadOnlyList<LayoutBounds> boxes)
  {
    if (boxes.Count == 0)
      return default;
    float left = float.MaxValue,
      top = float.MaxValue,
      right = float.MinValue,
      bottom = float.MinValue;
    foreach (LayoutBounds box in boxes)
    {
      left = Math.Min(left, box.X);
      top = Math.Min(top, box.Y);
      right = Math.Max(right, box.Right);
      bottom = Math.Max(bottom, box.Bottom);
    }
    return new LayoutBounds(left, top, right - left, bottom - top);
  }

  /// <summary>Moves every box to the same edge or center of the box they cover.</summary>
  public static void Align(IList<LayoutBounds> boxes, LayoutAlignment alignment)
  {
    if (boxes.Count < 2)
      return;
    LayoutBounds all = Union((IReadOnlyList<LayoutBounds>)boxes);
    for (int i = 0; i < boxes.Count; i++)
    {
      LayoutBounds box = boxes[i];
      float x = alignment switch
      {
        LayoutAlignment.Left => all.X,
        LayoutAlignment.Center => all.X + (all.Width - box.Width) * .5f,
        LayoutAlignment.Right => all.Right - box.Width,
        _ => box.X,
      };
      float y = alignment switch
      {
        LayoutAlignment.Top => all.Y,
        LayoutAlignment.Middle => all.Y + (all.Height - box.Height) * .5f,
        LayoutAlignment.Bottom => all.Bottom - box.Height,
        _ => box.Y,
      };
      boxes[i] = new LayoutBounds(x, y, box.Width, box.Height);
    }
  }

  /// <summary>
  /// Leaves the same gap between neighbours along one axis, in their current
  /// order, from one end of the covered box to the other. Boxes keep their
  /// place on the other axis. Needs three or more boxes.
  /// </summary>
  public static void Distribute(IList<LayoutBounds> boxes, bool horizontal)
  {
    int count = boxes.Count;
    if (count < 3)
      return;
    int[] order = new int[count];
    for (int i = 0; i < count; i++)
      order[i] = i;
    Array.Sort(
      order,
      (a, b) =>
      {
        int byStart = Start(boxes[a], horizontal).CompareTo(Start(boxes[b], horizontal));
        return byStart != 0 ? byStart : a.CompareTo(b);
      }
    );
    LayoutBounds all = Union((IReadOnlyList<LayoutBounds>)boxes);
    float total = 0;
    foreach (LayoutBounds box in boxes)
      total += Size(box, horizontal);
    float gap = (Size(all, horizontal) - total) / (count - 1);
    float position = Start(all, horizontal);
    foreach (int index in order)
    {
      LayoutBounds box = boxes[index];
      boxes[index] = horizontal
        ? new LayoutBounds(position, box.Y, box.Width, box.Height)
        : new LayoutBounds(box.X, position, box.Width, box.Height);
      position += Size(box, horizontal) + gap;
    }
  }

  private static float Start(LayoutBounds box, bool horizontal) => horizontal ? box.X : box.Y;

  private static float Size(LayoutBounds box, bool horizontal) => horizontal ? box.Width : box.Height;
}
