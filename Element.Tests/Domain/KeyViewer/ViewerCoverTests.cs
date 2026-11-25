using System.Collections.Generic;
using System.Numerics;
using Element.Configuration.Overlay;
using Element.Domain.KeyViewer;
using Element.Domain.Layout;
using Element.Tests.Support;
using static Element.Tests.Support.Assert;

namespace Element.Tests.Domain.KeyViewer;

/// <summary>
/// Covers: which elements are in front of a key viewer, the pixels a shape
/// covers, and how all covers of one viewer join into rectangles that never
/// overlap, coarser when there would be too many.
/// </summary>
internal static class ViewerCoverTests
{
  [Suite("key viewer covers: in front by list order, pixel-exact runs, disjoint rectangles, coarse when too many")]
  public static void Run()
  {
    FrontRule();
    DrawOrder();
    RectanglePixels();
    RingPixels();
    JoinedRectangles();
    TooManyRectangles();
    AgainWithoutAllocating();
  }

  // A combo changing every hit: the same viewer worked out again, once its buffers have grown.
  private static void AgainWithoutAllocating()
  {
    ViewerCoverMask mask = new();
    ViewerCoverRuns shape = new(),
      ring = new();
    List<LayoutBounds> spans = new();
    List<Vector2> outline = new() { new(40, 40), new(300, 60), new(280, 250), new(30, 200) };
    List<Vector2> rings = new()
    {
      new(100, 100),
      new(200, 100),
      new(200, 200),
      new(100, 200),
      new(130, 130),
      new(170, 130),
      new(170, 170),
      new(130, 170),
    };
    int[] one =  { 0 },
      two =  { 0, 4 };
    List<OverlayLayoutSettings> layouts = new() { Layout("a"), Layout("b") };
    OverlayDrawOrder order = new();
    order.Refresh(layouts);
    void Once()
    {
      shape.Reset(0, 0, 430, 270);
      shape.FillEvenOdd(outline, one);
      ring.Reset(0, 0, 430, 270);
      ring.FillEvenOdd(rings, two);
      ring.FillRect(350.5f, 10.5f, 60, 200);
      mask.Reset(0, 0, 430, 270);
      mask.Add(shape);
      mask.Add(ring);
      mask.Spans(spans);
      order.Refresh(layouts);
    }
    Once();
    long before = System.GC.GetAllocatedBytesForCurrentThread();
    for (int i = 0; i < 100; i++)
      Once();
    long allocated = System.GC.GetAllocatedBytesForCurrentThread() - before;
    Check(allocated == 0, "working a viewer's covers out again allocated " + allocated + " bytes");
  }

  private static void FrontRule()
  {
    Check(ViewerCoverRule.InFront(3, 1) && !ViewerCoverRule.InFront(1, 3), "a later entry is in front");
    Check(!ViewerCoverRule.InFront(2, 2), "an entry is not in front of itself");
    Check(
      !ViewerCoverRule.InFront(-1, 0) && !ViewerCoverRule.InFront(3, -1),
      "an entry the list lacks is never in front"
    );
    LayoutBounds viewer = new(100, 100, 200, 100);
    Check(ViewerCoverRule.Overlap(new LayoutBounds(250, 150, 100, 100), viewer), "overlapping areas overlap");
    Check(!ViewerCoverRule.Overlap(new LayoutBounds(300, 100, 50, 50), viewer), "areas that only touch don't");
    Check(!ViewerCoverRule.Overlap(new LayoutBounds(150, 150, 0, 50), viewer), "an empty area overlaps nothing");
    Check(ViewerCoverRule.Covers(2, new LayoutBounds(150, 150, 10, 10), 1, viewer), "in front and overlapping covers");
    Check(!ViewerCoverRule.Covers(0, new LayoutBounds(150, 150, 10, 10), 1, viewer), "behind it doesn't cover");
    Check(!ViewerCoverRule.Covers(2, new LayoutBounds(500, 500, 10, 10), 1, viewer), "elsewhere doesn't cover");
  }

  private static void DrawOrder()
  {
    OverlayLayoutSettings text = Layout("text"),
      hand = Layout("hand"),
      shape = Layout("shape");
    List<OverlayLayoutSettings> layouts = new() { text, hand, shape };
    OverlayDrawOrder order = new();
    Check(order.Refresh(layouts), "the first read is a change");
    Check(
      order.Position("text") == 0 && order.Position("hand") == 1 && order.Position("shape") == 2,
      "places follow the list"
    );
    Check(order.Position("missing") == -1 && order.Position(null) == -1, "an id the list lacks has no place");
    Check(!order.Refresh(layouts), "the same list is no change");
    layouts.Reverse();
    Check(
      order.Refresh(layouts) && order.Position("shape") == 0 && order.Position("text") == 2,
      "moving entries is a change"
    );
    shape.Enabled = false;
    Check(order.Refresh(layouts), "turning an entry off is a change");
    layouts.Add(Layout("text"));
    Check(order.Refresh(layouts) && order.Position("text") == 2, "the first entry of an id keeps its place");
    layouts.Clear();
    Check(order.Refresh(layouts) && order.Position("hand") == -1, "an emptied list is a change");
  }

  private static OverlayLayoutSettings Layout(string id) => new() { Id = id, Enabled = true };

  // A pixel counts when its center is inside; everything is kept to the clip.
  private static void RectanglePixels()
  {
    ViewerCoverRuns runs = new();
    runs.Reset(0, 0, 10, 10);
    runs.FillRect(.4f, .4f, 2.2f, 1.2f);
    Sequence(new[] { "0:0-3", "1:0-3" }, Runs(runs));
    runs.Reset(0, 0, 10, 10);
    runs.FillRect(.6f, .6f, 1, 1);
    Sequence(new[] { "1:1-2" }, Runs(runs));
    runs.Reset(0, 0, 10, 10);
    runs.FillRect(.6f, .6f, .3f, .3f);
    Check(runs.Count == 0, "a rectangle missing every pixel center covers none");
    runs.Reset(2, 2, 6, 5);
    runs.FillRect(-100, -100, 1000, 1000);
    Sequence(new[] { "2:2-6", "3:2-6", "4:2-6" }, Runs(runs));
    runs.Reset(0, 0, 10, 10);
    runs.FillRect(float.NaN, 0, 5, 5);
    runs.FillRect(0, 0, float.PositiveInfinity, 5);
    Check(runs.Count == 0, "rectangles that aren't numbers cover nothing");
    runs.Reset(0, 0, 10, 10);
    runs.Add(3, 1, 4);
    runs.Add(3, 4, 6);
    runs.Add(3, 8, 9);
    Sequence(new[] { "3:1-6", "3:8-9" }, Runs(runs));
    runs.Reset(0, 0, 10, 10);
    runs.FillQuad(new Vector2(1, 1), new Vector2(5, 1), new Vector2(5, 3), new Vector2(1, 3));
    Sequence(new[] { "1:1-5", "2:1-5" }, Runs(runs));
  }

  // Even-odd: an outline inside another cuts a hole, as a shape's border alone does.
  private static void RingPixels()
  {
    ViewerCoverRuns runs = new();
    runs.Reset(0, 0, 20, 20);
    List<Vector2> points = new()
    {
      new(0, 0),
      new(10, 0),
      new(10, 10),
      new(0, 10),
      new(3, 3),
      new(7, 3),
      new(7, 7),
      new(3, 7),
    };
    runs.FillEvenOdd(points, new[] { 0, 4 });
    string[] rows = Runs(runs);
    Check(rows.Length == 3 + 4 * 2 + 3, "a ring has whole rows above and below its hole, two runs beside it");
    Check(
      rows[0] == "0:0-10" && rows[3] == "3:0-3" && rows[4] == "3:7-10" && rows[13] == "9:0-10",
      "the hole is left out"
    );
    // A triangle: rows narrow toward its top corner, sampled at pixel centers.
    runs.Reset(0, 0, 20, 20);
    runs.FillEvenOdd(new List<Vector2> { new(5, 0), new(10, 10), new(0, 10) }, new[] { 0 });
    rows = Runs(runs);
    // At the tip's row the sides cross 4.75 and 5.25: no pixel center lies between.
    Check(rows.Length == 9 && rows[0] == "1:4-6" && rows[8] == "9:0-10", "a triangle widens downward from its tip");
  }

  private static void JoinedRectangles()
  {
    ViewerCoverMask mask = new();
    List<LayoutBounds> spans = new();
    // One square: one rectangle.
    mask.Reset(100, 200, 50, 40);
    mask.Add(Filled(100, 200, 150, 240, (runs) => runs.FillRect(110, 210, 10, 10)));
    mask.Spans(spans);
    Check(spans.Count == 1 && Same(spans[0], new LayoutBounds(110, 210, 10, 10)), "a square is one rectangle");
    // Two overlapping squares and a far one: the union, exactly, with no pixel twice.
    mask.Reset(0, 0, 64, 64);
    mask.Add(Filled(0, 0, 64, 64, (runs) => runs.FillRect(4, 4, 20, 20)));
    mask.Add(Filled(0, 0, 64, 64, (runs) => runs.FillRect(14, 14, 20, 20)));
    mask.Add(Filled(0, 0, 64, 64, (runs) => runs.FillRect(60, 1, 10, 3)));
    mask.Spans(spans);
    for (int y = 0; y < 64; y++)
    for (int x = 0; x < 64; x++)
    {
      bool inside =
        (x >= 4 && x < 24 && y >= 4 && y < 24)
        || (x >= 14 && x < 34 && y >= 14 && y < 34)
        || (x >= 60 && y >= 1 && y < 4);
      Check(Hits(spans, x, y) == (inside ? 1 : 0), "rectangles cover the union once, and nothing else");
    }
    Check(spans.Count == 4, "rows that line up join: two squares make three bands, plus the far one");
    // Pixels past the frame are left out; the frame's edge cuts the far one.
    Check(
      spans.TrueForAll(span => span.X >= 0 && span.Right <= 64 && span.Y >= 0 && span.Bottom <= 64),
      "rectangles stay in the frame"
    );
    mask.Reset(0, 0, 10, 10);
    mask.Spans(spans);
    Check(spans.Count == 0, "nothing covered is no rectangle");
  }

  // A checkerboard needs a rectangle per square; few allowed, the grid grows coarser.
  private static void TooManyRectangles()
  {
    ViewerCoverMask mask = new();
    List<LayoutBounds> spans = new();
    const int size = 100;
    mask.Reset(10, 20, size, size);
    ViewerCoverRuns board = Filled(
      10,
      20,
      10 + size,
      20 + size,
      (runs) =>
      {
        for (int y = 0; y < size; y++)
        for (int x = y % 2; x < size; x += 2)
          runs.Add(20 + y, 10 + x, 11 + x);
      }
    );
    mask.Add(board);
    mask.Spans(spans, 40);
    Check(spans.Count <= 40 && mask.Cell > 1, "too many rectangles make the grid coarser until they fit");
    for (int y = 0; y < size; y++)
    for (int x = 0; x < size; x++)
    {
      int hits = Hits(spans, 10 + x, 20 + y);
      Check(hits <= 1, "coarse rectangles still never overlap");
      if ((x + y) % 2 == 0)
        Check(hits == 1, "a coarse grid still covers every covered pixel");
    }
    Check(
      spans.TrueForAll(span => span.Right <= 10 + size && span.Bottom <= 20 + size),
      "coarse cells are cut at the frame"
    );
    // An odd-sized frame halves cleanly too; its last cells are cut at the frame.
    mask.Reset(0, 0, 7, 5);
    mask.Add(
      Filled(
        0,
        0,
        7,
        5,
        (runs) =>
        {
          for (int y = 0; y < 5; y++)
          for (int x = y % 2; x < 7; x += 2)
            runs.Add(y, x, x + 1);
        }
      )
    );
    mask.Spans(spans, 1);
    Check(spans.Count == 1 && Same(spans[0], new LayoutBounds(0, 0, 7, 5)), "odd frames coarsen within the frame");
    mask.Reset(0, 0, 131, 67);
    mask.Add(Filled(0, 0, 131, 67, (runs) => runs.FillRect(0, 0, 131, 67)));
    mask.Spans(spans, 1);
    Check(
      spans.Count == 1 && Same(spans[0], new LayoutBounds(0, 0, 131, 67)),
      "a full frame is one rectangle at any grid"
    );
  }

  private static ViewerCoverRuns Filled(int left, int top, int right, int bottom, System.Action<ViewerCoverRuns> fill)
  {
    ViewerCoverRuns runs = new();
    runs.Reset(left, top, right, bottom);
    fill(runs);
    return runs;
  }

  private static string[] Runs(ViewerCoverRuns runs)
  {
    string[] rows = new string[runs.Count];
    for (int i = 0; i < runs.Count; i++)
    {
      runs.Run(i, out int row, out int start, out int end);
      rows[i] = row + ":" + start + "-" + end;
    }
    return rows;
  }

  private static int Hits(List<LayoutBounds> spans, int x, int y)
  {
    int hits = 0;
    foreach (LayoutBounds span in spans)
      if (x + .5f >= span.X && x + .5f < span.Right && y + .5f >= span.Y && y + .5f < span.Bottom)
        hits++;
    return hits;
  }

  private static bool Same(LayoutBounds a, LayoutBounds b) =>
    a.X == b.X && a.Y == b.Y && a.Width == b.Width && a.Height == b.Height;
}
