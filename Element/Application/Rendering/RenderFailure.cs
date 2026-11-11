using System;

namespace Element.Application.Rendering;

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
