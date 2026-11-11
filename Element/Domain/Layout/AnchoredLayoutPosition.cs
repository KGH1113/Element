using Element.Configuration.Overlay;

namespace Element.Domain.Layout;

internal readonly struct AnchoredLayoutPosition
{
  public readonly Anchor9 Anchor;
  public readonly float OffsetX;
  public readonly float OffsetY;
  public bool IsValid =>
    OverlayLayoutPosition.ValidAnchor(Anchor)
    && OverlayLayoutGeometry.Finite(OffsetX)
    && OverlayLayoutGeometry.Finite(OffsetY);

  public AnchoredLayoutPosition(Anchor9 anchor, float offsetX, float offsetY)
  {
    Anchor = anchor;
    OffsetX = offsetX;
    OffsetY = offsetY;
  }
}
