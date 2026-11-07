using System;
using Element.Configuration;
using Element.Domain.Layout;

namespace Element.Domain.DmNote;

/// <summary>
/// Where a key viewer goes in a rendered video. The layout keeps the viewer's
/// anchored corner and scales with the frame's size relative to the game view,
/// the way the overlay's margins scale with the screen.
/// </summary>
internal sealed class RenderViewerPlacement
{
  private readonly OverlayLayoutSettings _anchor;
  private readonly float _centerX;
  private readonly float _centerY;

  private RenderViewerPlacement(
    string viewerKind,
    float scale,
    float viewportWidth,
    float viewportHeight,
    OverlayLayoutSettings anchor,
    float centerX,
    float centerY,
    int videoWidth,
    int videoHeight
  )
  {
    ViewerKind = viewerKind;
    Scale = scale;
    ViewportWidth = viewportWidth;
    ViewportHeight = viewportHeight;
    _anchor = anchor;
    _centerX = centerX;
    _centerY = centerY;
    VideoWidth = videoWidth;
    VideoHeight = videoHeight;
  }

  public string ViewerKind { get; }

  /// <summary>Video pixels per CSS pixel of the viewer's page.</summary>
  public float Scale { get; }

  /// <summary>A first guess at the page's CSS viewport.</summary>
  public float ViewportWidth { get; }
  public float ViewportHeight { get; }
  public int VideoWidth { get; }
  public int VideoHeight { get; }

  /// <param name="screenBounds">The viewer's content on screen, in pixels from the top left.</param>
  /// <param name="screenPixelsPerCssPixel">Screen pixels one CSS pixel of the page takes in the game.</param>
  /// <param name="anchor">The viewer's anchored placement, or null to keep its center.</param>
  public static RenderViewerPlacement Create(
    string viewerKind,
    LayoutBounds screenBounds,
    int screenWidth,
    int screenHeight,
    float screenPixelsPerCssPixel,
    OverlayLayoutSettings anchor,
    float viewportWidth,
    float viewportHeight,
    int videoWidth,
    int videoHeight
  )
  {
    if (
      screenWidth <= 0
      || screenHeight <= 0
      || videoWidth <= 0
      || videoHeight <= 0
      || !Positive(screenBounds.Width)
      || !Positive(screenBounds.Height)
      || !Positive(screenPixelsPerCssPixel)
      || !Positive(viewportWidth)
      || !Positive(viewportHeight)
    )
      return null;
    float scale = VideoScale(screenWidth, screenHeight, videoWidth, videoHeight) * screenPixelsPerCssPixel;
    OverlayLayoutSettings copy = null;
    if (anchor != null)
    {
      copy = new OverlayLayoutSettings();
      copy.CopyPlacementFrom(anchor);
    }
    return new RenderViewerPlacement(
      viewerKind,
      scale,
      viewportWidth,
      viewportHeight,
      copy,
      (screenBounds.X + screenBounds.Width * .5f) / screenWidth,
      (screenBounds.Y + screenBounds.Height * .5f) / screenHeight,
      videoWidth,
      videoHeight
    );
  }

  /// <summary>How much larger the video is than the game view, applied to sizes and margins alike.</summary>
  public static float VideoScale(int screenWidth, int screenHeight, int videoWidth, int videoHeight) =>
    (float)Math.Sqrt((double)videoWidth / screenWidth * ((double)videoHeight / screenHeight));

  /// <summary>
  /// The viewer's content, <paramref name="contentWidth"/> by <paramref name="contentHeight"/>
  /// CSS pixels, as fractions of the video from its top left. It covers whole video
  /// pixels, so a frame drawn at this viewer's scale shows one texel per pixel; a
  /// frame between pixels would be resampled and blur.
  /// </summary>
  public LayoutBounds Place(float contentWidth, float contentHeight)
  {
    float width = WholePixels(contentWidth * Scale);
    float height = WholePixels(contentHeight * Scale);
    LayoutBounds placed =
      _anchor != null
        ? OverlayLayoutPosition.Resolve(_anchor, width, height, VideoWidth, VideoHeight)
        : new LayoutBounds(_centerX * VideoWidth - width * .5f, _centerY * VideoHeight - height * .5f, width, height);
    placed = OverlayLayoutGeometry.KeepVisible(placed, VideoWidth, VideoHeight);
    return new LayoutBounds(
      (float)Math.Round(placed.X) / VideoWidth,
      (float)Math.Round(placed.Y) / VideoHeight,
      width / VideoWidth,
      height / VideoHeight
    );
  }

  private static float WholePixels(float pixels) => Math.Max(1, (float)Math.Round(pixels));

  private static bool Positive(float value) => !float.IsNaN(value) && !float.IsInfinity(value) && value > 0;
}
