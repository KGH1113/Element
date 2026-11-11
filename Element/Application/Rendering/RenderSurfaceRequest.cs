using System;

namespace Element.Application.Rendering;

/// <summary>One key viewer page for ElementNote to draw.</summary>
internal readonly struct RenderSurfaceRequest
{
  public RenderSurfaceRequest(string viewerKind, float scale, float viewportWidth, float viewportHeight)
  {
    ViewerKind = viewerKind;
    Scale = scale;
    ViewportWidth = viewportWidth;
    ViewportHeight = viewportHeight;
  }

  public string ViewerKind { get; }

  /// <summary>Video pixels per CSS pixel of the page.</summary>
  public float Scale { get; }

  /// <summary>A first guess at the page's CSS viewport; the page reports its own.</summary>
  public float ViewportWidth { get; }
  public float ViewportHeight { get; }
}
