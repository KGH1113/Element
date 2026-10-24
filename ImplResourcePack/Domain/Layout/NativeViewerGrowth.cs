using System;
using ImplResourcePack.Configuration;

namespace ImplResourcePack.Domain.Layout;

// Native viewers grow from a screen corner or their center, never an edge
// midpoint. Compare distances in actual game pixels, including on ultrawide.
internal static class NativeViewerGrowth
{
  private static readonly Anchor9[] Anchors =
  {
    Anchor9.Center,
    Anchor9.TopLeft,
    Anchor9.TopRight,
    Anchor9.BottomLeft,
    Anchor9.BottomRight,
  };

  public static bool IsAnchor(Anchor9 anchor) =>
    anchor == Anchor9.Center
    || anchor == Anchor9.TopLeft
    || anchor == Anchor9.TopRight
    || anchor == Anchor9.BottomLeft
    || anchor == Anchor9.BottomRight;

  public static Anchor9 SelectAnchor(LayoutBounds bounds, float viewportWidth, float viewportHeight)
  {
    if (
      !OverlayLayoutGeometry.Finite(bounds.X)
      || !OverlayLayoutGeometry.Finite(bounds.Y)
      || !OverlayLayoutGeometry.Finite(bounds.Width)
      || !OverlayLayoutGeometry.Finite(bounds.Height)
      || !OverlayLayoutGeometry.Finite(viewportWidth)
      || !OverlayLayoutGeometry.Finite(viewportHeight)
      || bounds.Width <= 0
      || bounds.Height <= 0
      || viewportWidth <= 0
      || viewportHeight <= 0
    )
      return Anchor9.Center;

    double centerX = bounds.X + (double)bounds.Width / 2;
    double centerY = bounds.Y + (double)bounds.Height / 2;
    double nearest = double.PositiveInfinity;
    Anchor9 selected = Anchor9.Center;
    foreach (Anchor9 anchor in Anchors)
    {
      double dx = centerX - OverlayLayoutPosition.AnchorX(anchor) * viewportWidth;
      double dy = centerY - OverlayLayoutPosition.AnchorY(anchor) * viewportHeight;
      double distance = dx * dx + dy * dy;
      // The center is first so exact ties have a stable, symmetric result.
      if (distance >= nearest)
        continue;
      nearest = distance;
      selected = anchor;
    }
    return selected;
  }

  public static OverlayLayoutSettings Capture(
    LayoutBounds bounds,
    float viewportWidth,
    float viewportHeight,
    float scale,
    Anchor9? anchor = null
  )
  {
    AnchoredLayoutPosition position = OverlayLayoutPosition.Capture(
      bounds,
      viewportWidth,
      viewportHeight,
      anchor ?? SelectAnchor(bounds, viewportWidth, viewportHeight)
    );
    if (!position.IsValid || !IsAnchor(position.Anchor) || !OverlayLayoutGeometry.Finite(scale) || scale <= 0)
      return null;
    return new OverlayLayoutSettings
    {
      Customized = true,
      LayoutVersion = OverlayLayoutPosition.CurrentVersion,
      Anchor = position.Anchor,
      OffsetX = position.OffsetX,
      OffsetY = position.OffsetY,
      Scale = scale,
    };
  }
}
