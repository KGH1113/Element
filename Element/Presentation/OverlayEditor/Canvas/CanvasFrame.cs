using Element.Application.Layout;

namespace Element.Presentation.OverlayEditor.Canvas;

/// <summary>An element in the layout editor and the outline drawn around it when selected.</summary>
internal sealed class CanvasFrame
{
  public readonly IOverlayLayoutTarget Target;
  public readonly CanvasOutlineGraphic Outline;

  public CanvasFrame(IOverlayLayoutTarget target, CanvasOutlineGraphic outline)
  {
    Target = target;
    Outline = outline;
  }
}
