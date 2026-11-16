using System;

namespace Element.Application.Rendering;

/// <summary>
/// A page's key viewer as a video ElementNote wrote: one picture per video frame,
/// from the rendered video's first frame, with alpha.
/// </summary>
internal sealed class RenderVideo
{
  public string ViewerKind { get; set; }
  public string Path { get; set; }
  public int Width { get; set; }
  public int Height { get; set; }
  public long Frames { get; set; }

  public bool IsValid() =>
    RenderSurface.IsViewerKind(ViewerKind)
    && !string.IsNullOrEmpty(Path)
    && Width > 0
    && Height > 0
    && Width <= 8192
    && Height <= 8192
    && Frames > 0;
}
