using System.Collections.Generic;
using Element.Application.KeyViewer;
using Element.Domain.Rendering;
using Element.Domain.KeyViewer;

namespace Element.Infrastructure.ElementNote.Viewers;

/// <summary>
/// Where the key viewers on screen go in a rendered video: each one where the game
/// shows it, at the size of its last page descriptor or of the sample.
/// </summary>
internal sealed class NativeViewerRenderPlacements
{
  private readonly NativeViewerTargets _targets;
  private readonly NativeViewerHostLifetime _host;

  public NativeViewerRenderPlacements(NativeViewerTargets targets, NativeViewerHostLifetime host)
  {
    _targets = targets;
    _host = host;
  }

  public List<RenderViewerPlacement> For(int videoWidth, int videoHeight)
  {
    var placements = new List<RenderViewerPlacement>();
    IReadOnlyList<NativeViewerLayoutTarget> targets = _targets.All;
    for (int i = 0; i < targets.Count; i++)
    {
      NativeViewerLayoutTarget target = targets[i];
      // The last page descriptor's viewport, or the sample's; the page reports its own.
      NativeViewerSurface known = _host.KnownSurface(target.Kind);
      RenderViewerPlacement placement = target.RenderPlacement(
        videoWidth,
        videoHeight,
        known?.Width ?? KeyViewerSampleSize.Width(target.Kind),
        known?.Height ?? KeyViewerSampleSize.ViewportHeight
      );
      if (placement != null)
        placements.Add(placement);
    }
    return placements;
  }
}
