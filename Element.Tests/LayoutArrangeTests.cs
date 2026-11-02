using System;
using System.Collections.Generic;
using Element.Domain.Layout;

namespace Element.Tests;

internal static class LayoutArrangeTests
{
  public static void Run()
  {
    List<LayoutBounds> boxes = Boxes();
    LayoutArrange.Align(boxes, LayoutAlignment.Left);
    Check(boxes.TrueForAll(box => box.X == 10), "align left lines every box up with the leftmost edge");
    Check(boxes[1].Y == 40 && boxes[1].Width == 30, "aligning on one axis keeps the other axis and the size");

    boxes = Boxes();
    LayoutArrange.Align(boxes, LayoutAlignment.Right);
    Check(boxes.TrueForAll(box => Same(box.Right, 200)), "align right lines up the right edges");

    boxes = Boxes();
    LayoutArrange.Align(boxes, LayoutAlignment.Center);
    Check(boxes.TrueForAll(box => Same(box.X + box.Width * .5f, 105)), "align center uses the middle of the covered box");

    boxes = Boxes();
    LayoutArrange.Align(boxes, LayoutAlignment.Bottom);
    Check(boxes.TrueForAll(box => Same(box.Bottom, 140)), "align bottom lines up the bottom edges");

    boxes = Boxes();
    LayoutArrange.Align(boxes, LayoutAlignment.Middle);
    Check(boxes.TrueForAll(box => Same(box.Y + box.Height * .5f, 70)), "align middle uses the vertical middle");

    // 100 of boxes in a 190 span (10..200) leaves two gaps of 45.
    boxes = Boxes();
    LayoutArrange.Distribute(boxes, true);
    Check(Same(boxes[0].X, 10) && Same(boxes[1].X, 75) && Same(boxes[2].X, 150), "distribute keeps the ends and evens out the gaps");
    Check(Same(boxes[1].X - boxes[0].Right, boxes[2].X - boxes[1].Right), "every gap is the same");
    Check(boxes[1].Y == 40, "distributing across keeps the other axis");

    // Order follows the current positions, not the list order.
    boxes = new List<LayoutBounds> { new(100, 0, 10, 10), new(0, 0, 10, 10), new(30, 0, 10, 10) };
    LayoutArrange.Distribute(boxes, true);
    Check(Same(boxes[1].X, 0) && Same(boxes[2].X, 50) && Same(boxes[0].X, 100), "distribute keeps the boxes in their order");

    boxes = new List<LayoutBounds> { new(0, 0, 10, 10), new(5, 5, 10, 10) };
    LayoutArrange.Distribute(boxes, false);
    Check(boxes[0].Y == 0 && boxes[1].Y == 5, "two boxes have nothing to distribute");
  }

  // 10..30 × 0..20, 60..90 × 40..60, 150..200 × 100..140.
  private static List<LayoutBounds> Boxes() =>
    new() { new(10, 0, 20, 20), new(60, 40, 30, 20), new(150, 100, 50, 40) };

  private static bool Same(float a, float b) => Math.Abs(a - b) < .001f;

  private static void Check(bool value, string message)
  {
    if (!value)
      throw new InvalidOperationException(message);
  }
}
