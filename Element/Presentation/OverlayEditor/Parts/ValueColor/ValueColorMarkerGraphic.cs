using Element.Presentation.Ui.Graphics;
using UnityEngine;
using UnityEngine.UI;

namespace Element.Presentation.OverlayEditor.Parts.ValueColor;

/// <summary>A small triangle filling its rect, pointing up at where a value sits on a band.</summary>
[RequireComponent(typeof(CanvasRenderer))]
internal sealed class ValueColorMarkerGraphic : MaskableGraphic
{
  protected override void OnPopulateMesh(VertexHelper mesh)
  {
    mesh.Clear();
    Rect rect = rectTransform.rect;
    if (rect.width <= 0 || rect.height <= 0)
      return;
    UiMeshShapes.Triangle(
      mesh,
      new Vector2(rect.center.x, rect.yMax),
      new Vector2(rect.xMin, rect.yMin),
      new Vector2(rect.xMax, rect.yMin),
      color,
      UiMeshShapes.Feather(this)
    );
  }
}
