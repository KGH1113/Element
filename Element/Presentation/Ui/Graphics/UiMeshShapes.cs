using UnityEngine;
using UnityEngine.UI;

namespace Element.Presentation.Ui.Graphics;

/// <summary>
/// Round shapes for hand-drawn graphics, each edge with a coverage fringe of
/// <c>feather</c> units so it stays smooth at any scale: Canvas UI gets no
/// MSAA. See <see cref="Feather"/> for one screen pixel.
/// </summary>
internal static class UiMeshShapes
{
  private const int Segments = 40;

  /// <summary>About one screen pixel in <paramref name="graphic"/>'s units.</summary>
  public static float Feather(Graphic graphic)
  {
    Canvas canvas = graphic.canvas;
    if (canvas == null)
      return .8f;
    float canvasScale = Mathf.Max(.00001f, Mathf.Abs(canvas.transform.lossyScale.x));
    float localScale = Mathf.Abs(graphic.transform.lossyScale.x) / canvasScale;
    return .9f / Mathf.Max(.1f, canvas.scaleFactor * localScale);
  }

  /// <summary>A filled circle; a wide <paramref name="feather"/> makes a soft one, such as a shadow.</summary>
  public static void Disc(VertexHelper mesh, Vector2 center, float radius, Color color, float feather)
  {
    Color clear = color;
    clear.a = 0;
    float inner = Mathf.Max(0, radius - feather * .5f),
      outer = radius + feather * .5f;
    int start = mesh.currentVertCount;
    mesh.AddVert(center, color, Vector2.zero);
    for (int i = 0; i < Segments; i++)
    {
      float angle = i * Mathf.PI * 2 / Segments;
      Vector2 direction = new(Mathf.Cos(angle), Mathf.Sin(angle));
      mesh.AddVert(center + direction * inner, color, Vector2.zero);
      mesh.AddVert(center + direction * outer, clear, Vector2.zero);
    }
    for (int i = 0; i < Segments; i++)
    {
      int current = start + 1 + i * 2,
        next = start + 1 + (i + 1) % Segments * 2;
      mesh.AddTriangle(start, current, next);
      mesh.AddTriangle(current, current + 1, next + 1);
      mesh.AddTriangle(current, next + 1, next);
    }
  }

  /// <summary>A ring <paramref name="width"/> wide, its outside at <paramref name="radius"/>.</summary>
  public static void Ring(VertexHelper mesh, Vector2 center, float radius, float width, Color color, float feather)
  {
    Color clear = color;
    clear.a = 0;
    float outerEdge = radius + feather * .5f,
      outerSolid = radius - feather * .5f,
      innerSolid = Mathf.Max(0, radius - width + feather * .5f),
      innerEdge = Mathf.Max(0, radius - width - feather * .5f);
    if (innerSolid > outerSolid)
      innerSolid = outerSolid = (innerSolid + outerSolid) * .5f;
    int start = mesh.currentVertCount;
    for (int i = 0; i < Segments; i++)
    {
      float angle = i * Mathf.PI * 2 / Segments;
      Vector2 direction = new(Mathf.Cos(angle), Mathf.Sin(angle));
      mesh.AddVert(center + direction * innerEdge, clear, Vector2.zero);
      mesh.AddVert(center + direction * innerSolid, color, Vector2.zero);
      mesh.AddVert(center + direction * outerSolid, color, Vector2.zero);
      mesh.AddVert(center + direction * outerEdge, clear, Vector2.zero);
    }
    for (int i = 0; i < Segments; i++)
    {
      int a = start + i * 4,
        b = start + (i + 1) % Segments * 4;
      for (int strip = 0; strip < 3; strip++)
      {
        mesh.AddTriangle(a + strip, b + strip, b + strip + 1);
        mesh.AddTriangle(a + strip, b + strip + 1, a + strip + 1);
      }
    }
  }

  /// <summary>A triangle with rounded-off coverage on its edges: <paramref name="a"/>, <paramref name="b"/>, <paramref name="c"/> counterclockwise or not.</summary>
  public static void Triangle(VertexHelper mesh, Vector2 a, Vector2 b, Vector2 c, Color color, float feather)
  {
    Color clear = color;
    clear.a = 0;
    Vector2 center = (a + b + c) / 3;
    int start = mesh.currentVertCount;
    Corner(mesh, a, center, color, clear, feather);
    Corner(mesh, b, center, color, clear, feather);
    Corner(mesh, c, center, color, clear, feather);
    mesh.AddTriangle(start, start + 2, start + 4);
    for (int i = 0; i < 3; i++)
    {
      int p = start + i * 2,
        q = start + (i + 1) % 3 * 2;
      mesh.AddTriangle(p, p + 1, q + 1);
      mesh.AddTriangle(p, q + 1, q);
    }
  }

  private static void Corner(VertexHelper mesh, Vector2 corner, Vector2 center, Color color, Color clear, float feather)
  {
    Vector2 outward = (corner - center).normalized * feather;
    mesh.AddVert(corner - outward * .5f, color, Vector2.zero);
    mesh.AddVert(corner + outward, clear, Vector2.zero);
  }
}
