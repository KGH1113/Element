using System;

namespace Element.Application.Rendering;

/// <summary>
/// Sends Element's render commands to the ElementNote that draws key viewers for
/// renders. Unity main thread.
/// </summary>
internal interface IKeyViewerRenderChannel
{
  /// <summary>The command's id for matching its reply, or null when no ElementNote takes renders.</summary>
  string Send(string name, object payload);
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

  /// <summary>"rgba" (raw rows, top first) or "png".</summary>
  public string FrameFormat { get; set; }
  public float ViewportWidth { get; set; }
  public float ViewportHeight { get; set; }

  /// <summary>CSS pixels around the content on each side, which the in-game viewer crops away.</summary>
  public float Padding { get; set; }

  public bool IsValid() =>
    (ViewerKind == "hand" || ViewerKind == "foot")
    && Width > 0
    && Height > 0
    && Width <= 8192
    && Height <= 8192
    && (FrameFormat == "rgba" || FrameFormat == "png")
    && Finite(ViewportWidth)
    && Finite(ViewportHeight)
    && ViewportWidth > 0
    && ViewportHeight > 0
    && Finite(Padding)
    && Padding >= 0
    && Padding * 2 < Math.Min(ViewportWidth, ViewportHeight);

  private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}

/// <summary>One drawn frame of one viewer, in a file ElementNote replaces with the next frame.</summary>
internal sealed class RenderFrame
{
  public string ViewerKind { get; set; }
  public string FramePath { get; set; }
}

/// <summary>ElementNote's answer to a begin or frame command.</summary>
internal sealed class RenderReply
{
  public string RequestId { get; set; }
  public bool Ok { get; set; }
  public string ErrorCode { get; set; }
  public string ErrorMessage { get; set; }
  public RenderSurface[] Surfaces { get; set; }
  public RenderFrame[] Frames { get; set; }
}

/// <summary>Why the key viewers left a render; <see cref="Code"/> picks the notice.</summary>
internal sealed class RenderFailure
{
  public const string AppUnavailable = "app-unavailable";
  public const string Timeout = "timeout";
  public const string Incompatible = "incompatible";
  public const string FrameUnreadable = "frame-unreadable";

  public RenderFailure(string code, string message, bool afterFirstFrame)
  {
    Code = string.IsNullOrEmpty(code) ? Incompatible : code;
    Message = message ?? string.Empty;
    AfterFirstFrame = afterFirstFrame;
  }

  public string Code { get; }
  public string Message { get; }

  /// <summary>The video has the key viewers until this point and lacks them after.</summary>
  public bool AfterFirstFrame { get; }
}
