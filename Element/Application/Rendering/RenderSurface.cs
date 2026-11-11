using System;

namespace Element.Application.Rendering;

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
