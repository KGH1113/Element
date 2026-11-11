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
