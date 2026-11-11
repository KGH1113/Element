using Element.Presentation.Layout.Graphics;

namespace Element.Presentation.Layout.Canvas;

/// <summary>An element in the layout editor and the outline drawn around it when selected.</summary>
internal sealed class LayoutFrame
{
  public readonly IOverlayLayoutTarget Target;
  public readonly LayoutOutlineGraphic Outline;

  public LayoutFrame(IOverlayLayoutTarget target, LayoutOutlineGraphic outline)
  {
    Target = target;
    Outline = outline;
  }
}
