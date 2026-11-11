using Element.Configuration.Overlay;
using Element.Domain.Layout;
using Element.Domain.Overlay.Shape;
using Element.Domain.Overlay.Values;
using Element.Presentation.Overlay.Elements;

namespace Element.Presentation.Overlay.Canvas;

/// <summary>Shapes: the bottom layer, behind other mods' elements and texts.</summary>
internal sealed class ShapeElementKind : OverlayElementKind<ShapeOverlayElement>
{
  private readonly OverlayCanvasHost _host;

  public ShapeElementKind(OverlayCanvasHost host) => _host = host;

  public override string LayerName => "Shapes";

  public override int LayerDepth => 0;

  public override bool Owns(string templateId) => templateId == OverlayElementKinds.Shape;

  protected override ShapeOverlayElement Make() => new(Layer, _host.Values);

  // Placed again: redrawn at the new size, edges kept one screen pixel soft.
  public override void Applied()
  {
    foreach (ShapeOverlayElement shape in Elements)
    {
      shape.Refresh(OverlayValueMask.None);
      shape.RefreshPixels();
    }
  }

  /// <summary>A new shape's saved layout.</summary>
  public static OverlayLayoutSettings NewSettings(OverlayShapeKind kind) =>
    new()
    {
      Id = OverlayElementIds.New(OverlayElementKinds.Shape),
      TemplateId = OverlayElementKinds.Shape,
      Shape = OverlayShapeGeometry.Create(kind),
    };
}
