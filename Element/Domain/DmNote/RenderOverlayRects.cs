using System;
using Element.Domain.Layout;

namespace Element.Domain.DmNote;

/// <summary>Whole pixels from the top left.</summary>
internal readonly struct PixelRect
{
  public PixelRect(int x, int y, int width, int height)
  {
    X = x;
    Y = y;
    Width = width;
    Height = height;
  }

  public int X { get; }
  public int Y { get; }
  public int Width { get; }
  public int Height { get; }

  public override string ToString() => X + "," + Y + " " + Width + "x" + Height;
}

/// <summary>
/// How one key viewer goes over a rendered video: the part of its page's video to
/// show (the page minus its padding, <see cref="Source"/>) and where it lands in the
/// rendered video (<see cref="Target"/>). A viewer drawn at its page's scale lands
/// one frame pixel per video pixel; a smaller viewer sharing the page gets a smaller
/// target, and the compositor scales it down.
/// </summary>
internal readonly struct RenderOverlayRects
{
  private RenderOverlayRects(PixelRect source, PixelRect target)
  {
    Source = source;
    Target = target;
  }

  /// <summary>In the page video's pixels.</summary>
  public PixelRect Source { get; }

  /// <summary>In the rendered video's pixels.</summary>
  public PixelRect Target { get; }

  /// <param name="pageScale">Video pixels per CSS pixel the page was drawn at: the largest scale of its viewers.</param>
  /// <param name="frameWidth">The page video's frame in pixels; ElementNote draws the whole viewport into it.</param>
  /// <param name="viewportWidth">The page's CSS viewport.</param>
  /// <param name="padding">CSS pixels around the content on each side.</param>
  public static RenderOverlayRects Of(
    RenderViewerPlacement placement,
    float pageScale,
    int frameWidth,
    int frameHeight,
    float viewportWidth,
    float viewportHeight,
    float padding
  )
  {
    if (placement == null)
      throw new ArgumentNullException(nameof(placement));
    if (!(pageScale > 0))
      throw new ArgumentOutOfRangeException(nameof(pageScale));
    RenderFrameCrop crop = RenderFrameCrop.Of(frameWidth, frameHeight, viewportWidth, viewportHeight, padding);
    // The target comes from the cropped pixels, not from CSS pixels again: a viewer drawn at
    // its page's scale takes them one for one. ElementNote and Element round a half pixel
    // differently, so recomputing could end a pixel off and make the compositor rescale.
    float ratio = placement.Scale / pageScale;
    int width = ratio == 1 ? crop.Width : Math.Max(1, Pixels(crop.Width * ratio));
    int height = ratio == 1 ? crop.Height : Math.Max(1, Pixels(crop.Height * ratio));
    LayoutBounds placed = placement.PlaceAtSize(width, height);
    return new RenderOverlayRects(
      new PixelRect(crop.X, crop.Y, crop.Width, crop.Height),
      new PixelRect(Pixels(placed.X * placement.VideoWidth), Pixels(placed.Y * placement.VideoHeight), width, height)
    );
  }

  // Place already snaps to whole pixels; this only removes float error.
  private static int Pixels(float value) => (int)Math.Round(value);
}
