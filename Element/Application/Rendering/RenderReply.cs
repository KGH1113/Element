using System;

namespace Element.Application.Rendering;

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
