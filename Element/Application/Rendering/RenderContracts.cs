using System;

namespace Element.Application.Rendering;

/// <summary>
/// Sends Element's render commands to the ElementNote that draws key viewers for
/// renders. Any thread: the render lease is kept from a timer.
/// </summary>
internal interface IKeyViewerRenderChannel
{
  /// <summary>The command's id for matching its reply, or null when no ElementNote takes renders.</summary>
  string Send(string name, object payload);
}

/// <summary>The ElementNote that draws key viewers for renders, as a render sees it.</summary>
internal interface IKeyViewerRenderApp : IKeyViewerRenderChannel
{
  /// <summary>An ElementNote that speaks Element's render protocol is connected.</summary>
  bool Available { get; }

  /// <summary>It can start a render's key counts from a run's recorded counts.</summary>
  bool TakesRecordedCounts { get; }

  /// <summary>Its answers to begin and finish.</summary>
  RenderReplies Replies { get; }
}

/// <summary>The video TUFReplay renders, as its ReplayApi reports it. Unity main thread.</summary>
internal interface IReplayRenderTarget
{
  /// <summary>The video's size while TUFReplay renders a run, otherwise 0.</summary>
  int RenderWidth { get; }
  int RenderHeight { get; }

  /// <summary>The video's frames per second while TUFReplay renders a run, otherwise 0.</summary>
  int RenderFps { get; }

  /// <summary>The renderer lays providers' videos over the finished video.</summary>
  bool RenderTakesOverlayVideos { get; }

  /// <summary>The FFmpeg TUFReplay shares with the renderer while it renders a run, or null.</summary>
  string RenderFfmpegPath { get; }
}

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

/// <summary>A page ElementNote prepared: its pixels and the viewport it laid out.</summary>
internal sealed class RenderSurface
{
  public string ViewerKind { get; set; }
  public int Width { get; set; }
  public int Height { get; set; }
  public float ViewportWidth { get; set; }
  public float ViewportHeight { get; set; }

  /// <summary>CSS pixels around the content on each side, which the in-game viewer crops away.</summary>
  public float Padding { get; set; }

  public bool IsValid() =>
    IsViewerKind(ViewerKind)
    && Width > 0
    && Height > 0
    && Width <= 8192
    && Height <= 8192
    && Finite(ViewportWidth)
    && Finite(ViewportHeight)
    && ViewportWidth > 0
    && ViewportHeight > 0
    && Finite(Padding)
    && Padding >= 0
    && Padding * 2 < Math.Min(ViewportWidth, ViewportHeight);

  internal static bool IsViewerKind(string kind) => kind == "hand" || kind == "foot";

  private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}

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

/// <summary>ElementNote's answer to a begin or finish command.</summary>
internal sealed class RenderReply
{
  public string RequestId { get; set; }
  public bool Ok { get; set; }
  public string ErrorCode { get; set; }
  public string ErrorMessage { get; set; }
  public RenderSurface[] Surfaces { get; set; }
  public RenderVideo[] Videos { get; set; }
}

/// <summary>Why a video has no key viewers; <see cref="Code"/> picks the notice.</summary>
internal sealed class RenderFailure
{
  public const string AppUnavailable = "app-unavailable";
  public const string AppOutdated = "app-outdated";
  public const string RendererOutdated = "renderer-outdated";
  public const string Timeout = "timeout";
  public const string Incompatible = "incompatible";

  public RenderFailure(string code, string message)
  {
    Code = string.IsNullOrEmpty(code) ? Incompatible : code;
    Message = message ?? string.Empty;
  }

  public string Code { get; }
  public string Message { get; }
}
