using Element.Domain.Elements;
using UnityEngine;

namespace Element.Presentation.Overlay.Elements;

/// <summary>
/// Where each built-in shape sits until it is moved, in 1920x1080 canvas
/// units. The progress bar follows JRP: centered just under the top, the
/// track and fill starting inside its left edge so the fill grows right.
/// </summary>
internal static class ShapeDefaultPlacement
{
  public static bool TryGet(string id, out Vector2 anchor, out Vector2 pivot, out Vector2 position)
  {
    anchor = new Vector2(.5f, 1);
    float top = -OverlayElementDefaults.ProgressBarTop,
      edge = OverlayElementDefaults.ProgressBarEdge;
    switch (id)
    {
      case OverlayElementDefaults.ProgressBarId:
        pivot = new Vector2(.5f, 1);
        position = new Vector2(0, top);
        return true;
      case OverlayElementDefaults.ProgressTrackId:
      case OverlayElementDefaults.ProgressFillId:
        pivot = new Vector2(0, 1);
        position = new Vector2(-OverlayElementDefaults.ProgressBarWidth * .5f + edge, top - edge);
        return true;
      default:
        pivot = position = default;
        return false;
    }
  }
}
