using Element.Configuration.Overlay;
using Element.Domain.KeyViewer;
using Element.Domain.Layout;
using static Element.Tests.Support.Assert;

namespace Element.Tests.Domain.KeyViewer;

/// <summary>Native viewers grow from a screen corner or their center, and shrink to fit.</summary>
internal static class NativeViewerGrowthTests
{
  internal static void Run()
  {
    NativeGrowth();
    NativeShrunkToFit();
  }

  private static void NativeGrowth()
  {
    foreach (
      var sample in new[]
      {
        (Anchor9.TopLeft, 100f, 100f),
        (Anchor9.TopRight, 1820f, 100f),
        (Anchor9.BottomLeft, 100f, 980f),
        (Anchor9.BottomRight, 1820f, 980f),
        (Anchor9.Center, 960f, 540f),
      }
    )
    {
      LayoutBounds original = new(sample.Item2 - 50, sample.Item3 - 30, 100, 60);
      OverlayLayoutSettings placement = NativeViewerGrowth.Capture(original, 1920, 1080, .75f);
      Check(placement?.Anchor == sample.Item1, "native viewer selects its nearest of five screen points");
      float ax = OverlayLayoutPosition.AnchorX(placement.Anchor);
      float ay = OverlayLayoutPosition.AnchorY(placement.Anchor);
      float fixedX = original.X + ax * original.Width;
      float fixedY = original.Y + ay * original.Height;
      foreach (var size in new[] { (800f, 500f), (1600f, 900f), (70f, 40f), (100f, 60f) })
      {
        LayoutBounds resized = OverlayLayoutPosition.Resolve(placement, size.Item1, size.Item2, 1920, 1080);
        Equal(fixedX, resized.X + ax * resized.Width, 0.001f, "growth and shrinkage retain the horizontal anchor");
        Equal(fixedY, resized.Y + ay * resized.Height, 0.001f, "growth and shrinkage retain the vertical anchor");
        Equal(size.Item1, resized.Width, 0.001f, "growth beyond 430px does not fit or shrink content");
        Equal(size.Item2, resized.Height, 0.001f, "growth beyond 270px does not fit or shrink content");
        Equal(.75f, placement.Scale, 0.001f, "automatic content changes preserve the user scale");
        Check(placement.Anchor == sample.Item1, "growing center cannot reselect the captured anchor");
      }
    }
    Check(
      NativeViewerGrowth.SelectAnchor(new(1950, 20, 100, 60), 4000, 500) == Anchor9.Center,
      "edge midpoints are not candidates, including on an ultrawide screen"
    );
    Check(
      NativeViewerGrowth.SelectAnchor(new(950, 70, 100, 60), 4000, 500) == Anchor9.TopLeft,
      "five-point selection compares actual pixel distances rather than normalized axes"
    );
    Check(
      NativeViewerGrowth.SelectAnchor(new(430, 240, 100, 60), 1920, 1080) == Anchor9.Center,
      "equal corner/center distance favors symmetric center growth"
    );
    OverlayLayoutSettings moved = NativeViewerGrowth.Capture(new(1750, 20, 100, 60), 1920, 1080, .75f);
    Check(moved.Anchor == Anchor9.TopRight, "moving a viewer selects a new growth direction");
    OverlayLayoutSettings kept = NativeViewerGrowth.Capture(
      new(400, 200, 1200, 600),
      1920,
      1080,
      .75f,
      Anchor9.BottomLeft
    );
    Check(
      kept.Anchor == Anchor9.BottomLeft,
      "manual resize preserves the selected growth direction even across center"
    );
    Check(
      NativeViewerGrowth.Capture(new(400, 200, 100, 60), 1920, 1080, .75f, Anchor9.Left) == null,
      "edge midpoints cannot enter native growth persistence"
    );
    Check(
      NativeViewerGrowth.Capture(new(0, 0, 0, 60), 1920, 1080, 1) == null,
      "unavailable native geometry cannot initialize growth"
    );
  }

  // A 600x200 viewer at 300% asked for 1000% shows at 320%, the most a
  // 1920x1080 view holds. The toolbar scales it at its center from there.
  private static void NativeShrunkToFit()
  {
    LayoutBounds shown = new(60, 400, 1800, 600);
    LayoutBounds grown = OverlayLayoutGeometry.ScaleAtCenter(shown, 10 / 3f, 1920, 1080, .2f / 3, 10 / 3f);
    OverlayLayoutSettings placement = NativeViewerGrowth.Capture(grown, 1920, 1080, 10, Anchor9.BottomLeft);
    LayoutBounds fitted = NativeViewerGrowth.Place(placement, 1920, 640, 1920, 1080, true);
    Check(
      fitted.X >= 0 && fitted.Y >= 0 && fitted.Right <= 1920.01f && fitted.Bottom <= 1080.01f,
      "a viewer shrunk to fit stays inside the view"
    );
    Equal(1080, fitted.Bottom, 0.001f, "a viewer shrunk to fit keeps to its anchored corner");
    LayoutBounds smaller = OverlayLayoutGeometry.ScaleAtCenter(fitted, 1 / 3.2f, 1920, 1080, .2f / 3.2f, 10 / 3.2f);
    Equal(600, smaller.Width, 0.001f, "a viewer shrunk to fit can be scaled down again");
    LayoutBounds unshrunk = NativeViewerGrowth.Place(placement, 600, 200, 1920, 1080, false);
    Equal(60, unshrunk.Right, 0.001f, "a viewer that fits may still sit mostly off screen");
  }
}
