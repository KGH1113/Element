using System.Collections.Generic;
using Element.Domain.Layout;
using Element.Tests.Support;
using static Element.Tests.Support.Assert;

namespace Element.Tests;

internal static class LayoutArrangeTests
{
  [Suite("aligns and distributes several layout boxes")]
  public static void Run()
  {
    List<LayoutBounds> boxes = Boxes();
    LayoutArrange.Align(boxes, LayoutAlignment.Left);
    Check(boxes.TrueForAll(box => box.X == 10), "align left lines every box up with the leftmost edge");
    Check(boxes[1].Y == 40 && boxes[1].Width == 30, "aligning on one axis keeps the other axis and the size");

    boxes = Boxes();
    LayoutArrange.Align(boxes, LayoutAlignment.Right);
    Check(boxes.TrueForAll(box => Near(box.Right, 200, .001f)), "align right lines up the right edges");

    boxes = Boxes();
    LayoutArrange.Align(boxes, LayoutAlignment.Center);
    Check(
      boxes.TrueForAll(box => Near(box.X + box.Width * .5f, 105, .001f)),
      "align center uses the middle of the covered box"
    );

    boxes = Boxes();
    LayoutArrange.Align(boxes, LayoutAlignment.Bottom);
    Check(boxes.TrueForAll(box => Near(box.Bottom, 140, .001f)), "align bottom lines up the bottom edges");

    boxes = Boxes();
    LayoutArrange.Align(boxes, LayoutAlignment.Middle);
    Check(boxes.TrueForAll(box => Near(box.Y + box.Height * .5f, 70, .001f)), "align middle uses the vertical middle");

    // 100 of boxes in a 190 span (10..200) leaves two gaps of 45.
    boxes = Boxes();
    LayoutArrange.Distribute(boxes, true);
    Check(
      Near(boxes[0].X, 10, .001f) && Near(boxes[1].X, 75, .001f) && Near(boxes[2].X, 150, .001f),
      "distribute keeps the ends and evens out the gaps"
    );
    Check(Near(boxes[1].X - boxes[0].Right, boxes[2].X - boxes[1].Right, .001f), "every gap is the same");
    Check(boxes[1].Y == 40, "distributing across keeps the other axis");

    // Order follows the current positions, not the list order.
    boxes = new List<LayoutBounds> { new(100, 0, 10, 10), new(0, 0, 10, 10), new(30, 0, 10, 10) };
    LayoutArrange.Distribute(boxes, true);
    Check(
      Near(boxes[1].X, 0, .001f) && Near(boxes[2].X, 50, .001f) && Near(boxes[0].X, 100, .001f),
      "distribute keeps the boxes in their order"
    );

    boxes = new List<LayoutBounds> { new(0, 0, 10, 10), new(5, 5, 10, 10) };
    LayoutArrange.Distribute(boxes, false);
    Check(boxes[0].Y == 0 && boxes[1].Y == 5, "two boxes have nothing to distribute");
  }

  // 10..30 × 0..20, 60..90 × 40..60, 150..200 × 100..140.
  private static List<LayoutBounds> Boxes() => new() { new(10, 0, 20, 20), new(60, 40, 30, 20), new(150, 100, 50, 40) };
}
