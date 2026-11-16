using System;
using Element.Configuration.Overlay;
using Element.Domain.KeyViewer;
using Element.Domain.Layout;
using Element.Domain.Rendering;
using static Element.Tests.Support.Assert;

namespace Element.Tests.Domain.Rendering;

/// <summary>
/// Where a key viewer goes in the video, and how its page is cropped and mapped
/// to video pixels.
/// </summary>
internal static class RenderPlacementTests
{
  internal static void Run()
  {
    PlacesViewersInTheVideo();
    CropsThePagePadding();
    MapsAFrameTexelForPixel();
    KeepsAHalfPixelPageOneToOne();
  }

  private static void PlacesViewersInTheVideo()
  {
    Equal(
      2,
      RenderViewerPlacement.VideoScale(960, 540, 1920, 1080),
      1e-4f,
      "A video twice the game view doubles everything."
    );
    // A 200x100 viewer 16 px from the bottom left of a 960x540 game view.
    var screen = new LayoutBounds(16, 540 - 100 - 16, 200, 100);
    OverlayLayoutSettings anchor = NativeViewerGrowth.Capture(screen, 960, 540, 1, Anchor9.BottomLeft);
    RenderViewerPlacement placement = RenderViewerPlacement.Create(
      "hand",
      screen,
      960,
      540,
      2,
      anchor,
      120,
      70,
      1920,
      1080
    );
    Equal(4, placement.Scale, 1e-4f, "Video pixels per CSS pixel: game pixels per CSS pixel times the video scale.");
    LayoutBounds placed = placement.Place(100, 50);
    Equal(400f / 1920, placed.Width, 1e-4f, "Content width in CSS pixels times the scale.");
    Equal(32f / 1920, placed.X, 1e-4f, "The anchored left margin doubles with the video.");
    Equal(1 - 32f / 1080, placed.Y + placed.Height, 1e-4f, "The bottom margin doubles too.");
    // A wider video keeps the corner; margins and size grow by the same factor.
    float k = RenderViewerPlacement.VideoScale(960, 540, 2560, 1080);
    RenderViewerPlacement wide = RenderViewerPlacement.Create("hand", screen, 960, 540, 2, anchor, 120, 70, 2560, 1080);
    LayoutBounds wider = wide.Place(100, 50);
    Equal(16 * k / 2560, wider.X, 1e-4f, "The left margin scales with the video.");
    Equal(1 - 16 * k / 1080, wider.Y + wider.Height, 1e-4f, "The bottom margin scales the same way.");
    Equal(100 * 2 * k / 2560, wider.Width, 1e-4f, "So does the viewer.");
    RenderViewerPlacement centered = RenderViewerPlacement.Create(
      "foot",
      screen,
      960,
      540,
      2,
      null,
      120,
      70,
      1920,
      1080
    );
    LayoutBounds middle = centered.Place(100, 50);
    Equal((16 + 100f) / 960, middle.X + middle.Width / 2, 1e-4f, "Without an anchor, the center stays.");
    Check(
      RenderViewerPlacement.Create("hand", screen, 0, 540, 2, anchor, 120, 70, 1920, 1080) == null,
      "No game view, no placement."
    );
  }

  private static void CropsThePagePadding()
  {
    RenderFrameCrop crop = RenderFrameCrop.Of(200, 100, 200, 100, 10);
    Check(crop.X == 10 && crop.Y == 10 && crop.Width == 180 && crop.Height == 80, "The padding goes on every side.");
    // 30 CSS px of padding at 0.75 px per CSS px is 22.5 texels.
    RenderFrameCrop half = RenderFrameCrop.Of(645, 240, 860, 320, 30);
    Check(half.X == 22 && half.Width == 601, "A crop between texels moves to a whole texel.");
    Check(half.Y == 22 && half.Height == 196, "So does the vertical crop.");
    Equal(601 / .75f, half.CssWidth, 1e-4f, "The shown texels in CSS pixels.");
    RenderFrameCrop tiny = RenderFrameCrop.Of(3, 3, 3, 3, 1.4f);
    Check(tiny.Width == 1 && tiny.Height == 1, "At least one texel stays.");
  }

  // A 5K Retina game view (2 screen pixels per CSS pixel) rendered to 1080p draws the
  // viewer at 0.75 video pixels per CSS pixel. ElementNote draws the 860x320 page into
  // 645x240 pixels; the video must show those pixels one to one, or they blur.
  private static void MapsAFrameTexelForPixel()
  {
    var screen = new LayoutBounds(17.3f, 2880 - 600 - 41.7f, 1600, 600);
    OverlayLayoutSettings anchor = NativeViewerGrowth.Capture(screen, 5120, 2880, 1, Anchor9.BottomLeft);
    RenderViewerPlacement placement = RenderViewerPlacement.Create(
      "hand",
      screen,
      5120,
      2880,
      2,
      anchor,
      860,
      320,
      1920,
      1080
    );
    Equal(.75f, placement.Scale, 1e-4f, "Video pixels per CSS pixel.");
    RenderFrameCrop crop = RenderFrameCrop.Of(645, 240, 860, 320, 30);
    LayoutBounds placed = placement.Place(crop.CssWidth, crop.CssHeight);
    Pixels(crop.Width, placed.Width * 1920, "One video pixel per texel across.");
    Pixels(crop.Height, placed.Height * 1080, "And down.");
    Whole(placed.X * 1920, "The left edge sits on a pixel boundary.");
    Whole(placed.Y * 1080, "So does the top edge.");
    LayoutBounds centered = RenderViewerPlacement
      .Create("foot", screen, 5120, 2880, 2, null, 860, 320, 1920, 1080)
      .Place(crop.CssWidth, crop.CssHeight);
    Whole(centered.X * 1920, "A centered viewer snaps too.");
    Whole(centered.Y * 1080, "Vertically as well.");

    RenderOverlayRects rects = RenderOverlayRects.Of(placement, placement.Scale, 645, 240, 860, 320, 30);
    Check(
      rects.Source.X == 22 && rects.Source.Y == 22 && rects.Source.Width == 601 && rects.Source.Height == 196,
      "The video shows the page minus its padding: " + rects.Source
    );
    Check(
      rects.Target.Width == rects.Source.Width && rects.Target.Height == rects.Source.Height,
      "And lays it one pixel per pixel: " + rects.Target
    );
    Check(
      rects.Target.X == (int)Math.Round(placed.X * 1920) && rects.Target.Y == (int)Math.Round(placed.Y * 1080),
      "At the viewer's place: " + rects.Target
    );
    Check(rects.Target.Y + rects.Target.Height <= 1080 && rects.Target.X >= 0, "Inside the video.");
  }

  // A 200 CSS px page at 0.5625 is 112.5 pixels: ElementNote rounds it up to 113, while
  // C# rounds the same half down to 112. Without padding, a target recomputed from CSS
  // pixels came out a pixel narrower than the page, and the compositor rescaled every frame.
  private static void KeepsAHalfPixelPageOneToOne()
  {
    var screen = new LayoutBounds(100, 100, 400, 240);
    RenderViewerPlacement placement = RenderViewerPlacement.Create(
      "foot",
      screen,
      5120,
      2880,
      2,
      null,
      200,
      120,
      1440,
      810
    );
    Equal(.5625f, placement.Scale, 1e-4f, "Video pixels per CSS pixel.");
    RenderOverlayRects rects = RenderOverlayRects.Of(placement, placement.Scale, 113, 68, 200, 120, 0);
    Check(
      rects.Source.Width == 113 && rects.Target.Width == 113 && rects.Target.Height == rects.Source.Height,
      "A viewer at its page's scale takes the page's pixels one for one: " + rects.Source + " -> " + rects.Target
    );
  }

  private static void Pixels(float expected, float actual, string message) =>
    Check(Math.Abs(expected - actual) < 1e-3, message + " Expected " + expected + ", got " + actual + ".");

  private static void Whole(float pixels, string message) => Pixels((float)Math.Round(pixels), pixels, message);
}
