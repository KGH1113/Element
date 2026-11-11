using UnityEngine;
using UnityEngine.UI;

namespace Element.Presentation.Settings;

// A geometric stencil avoids magnifying the few corner texels in the shared
// small nine-slice sprite across the large saturation/value surface.
[RequireComponent(typeof(CanvasRenderer))]
internal sealed class SettingsColorMask : MaskableGraphic
{
  protected override void OnPopulateMesh(VertexHelper mesh)
  {
    mesh.Clear();
    Rect r = rectTransform.rect;
    float radius = Mathf.Min(12, Mathf.Min(r.width, r.height) * .5f);
    mesh.AddVert(r.center, color, Vector2.zero);
    const int segments = 12;
    int count = 0;
    for (int corner = 0; corner < 4; corner++)
    {
      Vector2 center = new(
        corner == 0 || corner == 3 ? r.xMax - radius : r.xMin + radius,
        corner < 2 ? r.yMax - radius : r.yMin + radius
      );
      for (int step = 0; step <= segments; step++)
      {
        float angle = (corner + step / (float)segments) * Mathf.PI * .5f;
        mesh.AddVert(center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius, color, Vector2.zero);
        if (count > 0)
          mesh.AddTriangle(0, count, count + 1);
        count++;
      }
    }
    mesh.AddTriangle(0, count, 1);
  }
}
