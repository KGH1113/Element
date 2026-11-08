using System;
using System.Collections.Generic;
using Element.Domain.DmNote;

namespace Element.Application.Rendering;

/// <summary>One key viewer laid over a rendered video.</summary>
internal readonly struct KeyViewerOverlay
{
  public KeyViewerOverlay(string path, PixelRect source, PixelRect target)
  {
    Path = path;
    Source = source;
    Target = target;
  }

  /// <summary>The page video ElementNote wrote.</summary>
  public string Path { get; }

  /// <summary>The part of each frame to show, in the page video's pixels.</summary>
  public PixelRect Source { get; }

  /// <summary>Where it goes, in the rendered video's pixels.</summary>
  public PixelRect Target { get; }
}

/// <summary>Whether a render can have key viewers, and how they go over the finished video.</summary>
internal static class KeyViewerRenderPlan
{
  /// <summary>
  /// Why this render can't have key viewers, or null when it can. The renderer comes first:
  /// opening or updating ElementNote does not help while TUFReplay or OrbitRender can't take
  /// the videos.
  /// </summary>
  /// <param name="appAvailable">An ElementNote that speaks Element's render protocol is connected.</param>
  /// <param name="appConnected">ElementNote is connected for the in-game viewers, whatever its version.</param>
  public static RenderFailure Blocker(IReplayRenderTarget target, bool appAvailable, bool appConnected)
  {
    if (target == null)
      throw new ArgumentNullException(nameof(target));
    if (!target.RenderTakesOverlayVideos || string.IsNullOrEmpty(target.RenderFfmpegPath) || target.RenderFps <= 0)
      return new RenderFailure(
        RenderFailure.RendererOutdated,
        "TUFReplay or OrbitRender can't lay key viewer videos over the render."
      );
    if (!appAvailable)
      return appConnected
        ? new RenderFailure(RenderFailure.AppOutdated, "ElementNote does not draw key viewers for renders.")
        : new RenderFailure(RenderFailure.AppUnavailable, "ElementNote is not running.");
    return null;
  }

  /// <summary>
  /// Every placed viewer over its page's video. Several viewers of a kind share one page;
  /// a viewer drawn smaller than its page gets a smaller target.
  /// </summary>
  public static IReadOnlyList<KeyViewerOverlay> Overlays(
    IReadOnlyList<RenderViewerPlacement> viewers,
    IReadOnlyList<RenderSurface> surfaces,
    IReadOnlyList<RenderVideo> videos
  )
  {
    var overlays = new List<KeyViewerOverlay>(viewers?.Count ?? 0);
    if (viewers == null || surfaces == null || videos == null)
      return overlays;
    foreach (RenderViewerPlacement viewer in viewers)
    {
      RenderSurface surface = Find(surfaces, viewer.ViewerKind);
      RenderVideo video = Find(videos, viewer.ViewerKind);
      if (surface == null || video == null)
        continue;
      // The video's own size: ElementNote draws the whole viewport into each frame, at the
      // largest scale of the page's viewers.
      RenderOverlayRects rects = RenderOverlayRects.Of(
        viewer,
        PageScale(viewers, viewer.ViewerKind),
        video.Width,
        video.Height,
        surface.ViewportWidth,
        surface.ViewportHeight,
        surface.Padding
      );
      overlays.Add(new KeyViewerOverlay(video.Path, rects.Source, rects.Target));
    }
    return overlays;
  }

  /// <summary>The scale a kind's page is drawn at: the largest of its viewers'.</summary>
  public static float PageScale(IEnumerable<RenderViewerPlacement> viewers, string kind)
  {
    float scale = 0;
    foreach (RenderViewerPlacement viewer in viewers)
      if (viewer.ViewerKind == kind && viewer.Scale > scale)
        scale = viewer.Scale;
    return scale;
  }

  private static RenderSurface Find(IReadOnlyList<RenderSurface> surfaces, string kind)
  {
    foreach (RenderSurface surface in surfaces)
      if (surface?.ViewerKind == kind)
        return surface;
    return null;
  }

  private static RenderVideo Find(IReadOnlyList<RenderVideo> videos, string kind)
  {
    foreach (RenderVideo video in videos)
      if (video?.ViewerKind == kind)
        return video;
    return null;
  }
}
