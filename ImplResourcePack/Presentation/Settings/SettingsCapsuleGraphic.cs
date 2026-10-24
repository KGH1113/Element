using UnityEngine;
using UnityEngine.UI;

namespace ImplResourcePack.Presentation.Settings;

// Small tracks and circular thumbs cannot share the large panels' sliced sprite:
// squeezing its borders distorts the end caps. Build a fixed-radius capsule instead.
[RequireComponent(typeof(CanvasRenderer))]
internal sealed class SettingsCapsuleGraphic : MaskableGraphic
{
  protected override void OnPopulateMesh(VertexHelper mesh)
  {
    mesh.Clear();
    Rect rect = rectTransform.rect;
    if (rect.width <= 0 || rect.height <= 0)
      return;

    const int segments = 8;
    const int count = (segments + 1) * 4;
    float radius = Mathf.Min(rect.width, rect.height) * .5f;
    float feather = Mathf.Min(.75f, radius);
    Color transparent = color;
    transparent.a = 0;
    mesh.AddVert(rect.center, color, Vector2.zero);
    for (int corner = 0; corner < 4; corner++)
    {
      Vector2 center = new(
        corner == 0 || corner == 3 ? rect.xMax - radius : rect.xMin + radius,
        corner < 2 ? rect.yMax - radius : rect.yMin + radius
      );
      for (int step = 0; step <= segments; step++)
      {
        float angle = (corner + (float)step / segments) * Mathf.PI * .5f;
        Vector2 direction = new(Mathf.Cos(angle), Mathf.Sin(angle));
        mesh.AddVert(center + direction * (radius - feather * .5f), color, Vector2.zero);
        mesh.AddVert(center + direction * (radius + feather * .5f), transparent, Vector2.zero);
      }
    }
    for (int i = 0; i < count; i++)
    {
      int current = 1 + i * 2;
      int next = 1 + (i + 1) % count * 2;
      mesh.AddTriangle(0, current, next);
      mesh.AddTriangle(current, current + 1, next + 1);
      mesh.AddTriangle(current, next + 1, next);
    }
  }
}
