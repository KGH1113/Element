using System;
using Element.Domain.Layout;
using static Element.Tests.Support.Assert;

namespace Element.Tests.Domain.Layout;

/// <summary>
/// The eight resize handles keep the ratio and the opposite side, moving is one to
/// one, a sliver always stays on screen, and bad pointers change nothing.
/// </summary>
internal static class LayoutResizeTests
{
  internal static void Run()
  {
    ResizesFromEachHandle();
    MovesOneToOne();
    RandomResizesStayVisible();
    HitsEachHandle();
    IgnoresInvalidPointers();
  }

  private static readonly LayoutBounds Start = new(100f, 80f, 200f, 100f);

  private static readonly LayoutHandle[] Handles =
  {
    LayoutHandle.Left,
    LayoutHandle.Right,
    LayoutHandle.Top,
    LayoutHandle.Bottom,
    LayoutHandle.TopLeft,
    LayoutHandle.TopRight,
    LayoutHandle.BottomLeft,
    LayoutHandle.BottomRight,
  };

  private static void ResizesFromEachHandle()
  {
    foreach (LayoutHandle handle in Handles)
    {
      bool left = handle == LayoutHandle.Left || handle == LayoutHandle.TopLeft || handle == LayoutHandle.BottomLeft;
      bool right =
        handle == LayoutHandle.Right || handle == LayoutHandle.TopRight || handle == LayoutHandle.BottomRight;
      bool top = handle == LayoutHandle.Top || handle == LayoutHandle.TopLeft || handle == LayoutHandle.TopRight;
      bool bottom =
        handle == LayoutHandle.Bottom || handle == LayoutHandle.BottomLeft || handle == LayoutHandle.BottomRight;
      LayoutBounds result = OverlayLayoutGeometry.Drag(
        Start,
        handle,
        left ? -40f
          : right ? 40f
          : 0f,
        top ? -20f
          : bottom ? 20f
          : 0f,
        1920f,
        1080f
      );
      Equal(240f, result.Width, 0.001f, handle + " grows in the grabbed direction");
      Equal(120f, result.Height, 0.001f, handle + " preserves aspect ratio");
      if (left)
        Equal(Start.Right, result.Right, 0.001f, handle + " anchors right");
      else if (right)
        Equal(Start.X, result.X, 0.001f, handle + " anchors left");
      else
        Equal(Start.X + Start.Width / 2f, result.X + result.Width / 2f, 0.001f, handle + " anchors horizontal center");
      if (top)
        Equal(Start.Bottom, result.Bottom, 0.001f, handle + " anchors bottom");
      else if (bottom)
        Equal(Start.Y, result.Y, 0.001f, handle + " anchors top");
      else
        Equal(Start.Y + Start.Height / 2f, result.Y + result.Height / 2f, 0.001f, handle + " anchors vertical center");

      LayoutBounds stationary = OverlayLayoutGeometry.Drag(Start, handle, 0f, 0f, 1920f, 1080f);
      Equal(Start.X, stationary.X, 0.001f, "pointer-down must not snap to an anchor");
      Equal(Start.Y, stationary.Y, 0.001f, "pointer-down must preserve grab offset");
      Equal(Start.Width, stationary.Width, 0.001f, "zero delta keeps scale");
    }

    LayoutBounds shrunk = OverlayLayoutGeometry.Drag(Start, LayoutHandle.TopLeft, 2000f, 2000f, 1920f, 1080f);
    Equal(40f, shrunk.Width, 0.001f, "minimum scale prevents inverted bounds");
    Equal(Start.Right, shrunk.Right, 0.001f, "minimum clamp preserves opposite X anchor");
    Equal(Start.Bottom, shrunk.Bottom, 0.001f, "minimum clamp preserves opposite Y anchor");
    LayoutBounds enlarged = OverlayLayoutGeometry.Drag(
      Start,
      LayoutHandle.BottomRight,
      20000f,
      20000f,
      1920f,
      1080f,
      0.1f,
      2f
    );
    Equal(400f, enlarged.Width, 0.001f, "absolute scale limit supplied relative to drag start");
    Equal(Start.X, enlarged.X, 0.001f, "maximum clamp preserves opposite anchor");
    LayoutBounds nearEdge = new(800f, 400f, 200f, 100f);
    LayoutBounds viewport = OverlayLayoutGeometry.Drag(nearEdge, LayoutHandle.BottomRight, 1000f, 1000f, 1024f, 600f);
    Equal(1000f, viewport.Width, 0.001f, "growth can extend beyond the viewport");
    Equal(500f, viewport.Height, 0.001f, "offscreen growth preserves ratio");
    Equal(800f, viewport.X, 0.001f, "viewport clamp does not move opposite corner");
    Equal(400f, viewport.Y, 0.001f, "viewport clamp does not move opposite corner vertically");
    viewport = OverlayLayoutGeometry.Drag(nearEdge, LayoutHandle.Left, -1000f, 0f, 1024f, 600f);
    Equal(1000f, viewport.Width, 0.001f, "edge drag can extend beyond its other axis");
    Equal(1000f, viewport.Right, 0.001f, "edge growth preserves opposite edge");
    Equal(450f, viewport.Y + viewport.Height * 0.5f, 0.001f, "edge growth preserves other axis center");
  }

  private static void MovesOneToOne()
  {
    LayoutBounds moved = OverlayLayoutGeometry.Drag(Start, LayoutHandle.Move, 35f, -20f, 1920f, 1080f);
    Equal(135f, moved.X, 0.001f, "drag is one-to-one in X");
    Equal(60f, moved.Y, 0.001f, "drag is one-to-one in Y");
    moved = OverlayLayoutGeometry.Drag(Start, LayoutHandle.Move, -999f, 9999f, 1920f, 1080f);
    Equal(-180f, moved.X, 0.001f, "left edge keeps 10 percent visible");
    Equal(1070f, moved.Y, 0.001f, "bottom edge keeps 10 percent visible");
    moved = OverlayLayoutGeometry.Drag(Start, LayoutHandle.Move, 99999, -99999, 1920, 1080);
    Equal(1900, moved.X, 0.001f, "right edge keeps 10 percent visible");
    Equal(-90, moved.Y, 0.001f, "top edge keeps 10 percent visible");
  }

  private static void RandomResizesStayVisible()
  {
    // Exercise every fixed anchor from partially offscreen starting positions.
    // Shrinking is just as important: it must not hide the last visible sliver.
    Random random = new(721);
    for (int i = 0; i < 2000; i++)
    {
      LayoutBounds sample = OverlayLayoutGeometry.KeepVisible(
        new LayoutBounds(
          (float)random.NextDouble() * 2400 - 800,
          (float)random.NextDouble() * 1400 - 400,
          20 + (float)random.NextDouble() * 900,
          10 + (float)random.NextDouble() * 500
        ),
        1024,
        600
      );
      LayoutHandle handle = Handles[i % Handles.Length];
      LayoutBounds result = OverlayLayoutGeometry.Drag(
        sample,
        handle,
        (float)random.NextDouble() * 10000 - 5000,
        (float)random.NextDouble() * 10000 - 5000,
        1024,
        600
      );
      Visible(result, 1024, 600);
      Equal(sample.Width / sample.Height, result.Width / result.Height, 0.001f, "random resize preserves ratio");
      LayoutBounds scaled = OverlayLayoutGeometry.ScaleAtCenter(sample, i % 2 == 0 ? .1f : 50f, 1024, 600, .2f, 5f);
      Visible(scaled, 1024, 600);
      Equal(sample.X + sample.Width * .5f, scaled.X + scaled.Width * .5f, 0.001f, "size input preserves center");
    }
  }

  private static void HitsEachHandle()
  {
    Hit(LayoutHandle.TopLeft, Start, 103f, 83f);
    Hit(LayoutHandle.TopRight, Start, 298f, 78f);
    Hit(LayoutHandle.BottomLeft, Start, 99f, 181f);
    Hit(LayoutHandle.BottomRight, Start, 302f, 182f);
    Hit(LayoutHandle.Top, Start, 200f, 82f);
    Hit(LayoutHandle.Bottom, Start, 200f, 181f);
    Hit(LayoutHandle.Left, Start, 102f, 130f);
    Hit(LayoutHandle.Right, Start, 298f, 130f);
    Hit(LayoutHandle.Move, Start, 200f, 130f);
    Hit(LayoutHandle.None, Start, 80f, 130f);
  }

  private static void IgnoresInvalidPointers()
  {
    LayoutBounds invalid = OverlayLayoutGeometry.Drag(Start, LayoutHandle.Left, float.NaN, 0f, 1920f, 1080f);
    Equal(Start.Width, invalid.Width, 0.001f, "invalid pointer positions cannot poison saved layout");
    LayoutBounds empty = OverlayLayoutGeometry.Drag(
      new LayoutBounds(1f, 2f, 0f, 0f),
      LayoutHandle.Right,
      30f,
      0f,
      1920f,
      1080f
    );
    Equal(0f, empty.Width, 0.001f, "unavailable zero-sized targets are safe");
  }

  private static void Hit(LayoutHandle expected, LayoutBounds bounds, float x, float y)
  {
    LayoutHandle actual = OverlayLayoutGeometry.HitTest(bounds, x, y);
    if (actual != expected)
      throw new InvalidOperationException("Expected " + expected + " at pointer, got " + actual);
  }

  private static void Visible(LayoutBounds value, float width, float height)
  {
    float visibleWidth = Math.Min(width, value.Right) - Math.Max(0, value.X);
    float visibleHeight = Math.Min(height, value.Bottom) - Math.Max(0, value.Y);
    if (visibleWidth + .002f < value.Width * .1f || visibleHeight + .002f < value.Height * .1f)
      throw new InvalidOperationException("Resize must keep at least 10 percent of both axes visible");
  }
}
