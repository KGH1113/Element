using UnityEngine;
using UnityEngine.UI;

namespace Element.Presentation.Settings;

// Continuous stroke strips with a one-screen-pixel coverage fringe. Canvas UI
// does not receive the camera's MSAA, so hard-edged triangles alone shimmer at
// small UI scales. Each graphic retains its path buffer for the next rebuild.
[RequireComponent(typeof(CanvasRenderer))]
internal abstract class SettingsVectorGraphic : MaskableGraphic
{
  private readonly Vector2[] _points = new Vector2[96];
  private int _count;
  protected virtual float DesignSize => 24;

  protected void BeginPath() => _count = 0;

  protected void PathPoint(float x, float y)
  {
    Rect rect = rectTransform.rect;
    float unit = Mathf.Min(rect.width, rect.height) / DesignSize;
    _points[_count++] = rect.center + new Vector2(x - DesignSize * .5f, DesignSize * .5f - y) * unit;
  }

  protected void Stroke(VertexHelper mesh, float width = 2, bool closed = false)
  {
    if (_count < 2)
      return;
    float unit = Mathf.Min(rectTransform.rect.width, rectTransform.rect.height) / DesignSize;
    float feather = CoverageWidth();
    float half = Mathf.Max(width * unit * .5f, feather * .65f);
    float inner = Mathf.Max(.05f, half - feather * .5f),
      outer = half + feather * .5f;
    int start = mesh.currentVertCount;
    Color clear = color;
    clear.a = 0;
    for (int i = 0; i < _count; i++)
    {
      Vector2 previous = _points[i == 0 ? (closed ? _count - 1 : 0) : i - 1];
      Vector2 next = _points[i == _count - 1 ? (closed ? 0 : i) : i + 1];
      Vector2 before = (_points[i] - previous).normalized;
      Vector2 after = (next - _points[i]).normalized;
      if (before == Vector2.zero)
        before = after;
      if (after == Vector2.zero)
        after = before;
      Vector2 normal = new(-(before.y + after.y), before.x + after.x);
      normal.Normalize();
      float miter = Mathf.Min(1.7f, 1 / Mathf.Max(.1f, Vector2.Dot(normal, new Vector2(-after.y, after.x))));
      normal *= miter;
      mesh.AddVert(_points[i] + normal * outer, clear, Vector2.zero);
      mesh.AddVert(_points[i] + normal * inner, color, Vector2.zero);
      mesh.AddVert(_points[i] - normal * inner, color, Vector2.zero);
      mesh.AddVert(_points[i] - normal * outer, clear, Vector2.zero);
      if (i > 0)
        Connect(mesh, start + (i - 1) * 4, start + i * 4);
    }
    if (closed)
      Connect(mesh, start + (_count - 1) * 4, start);
    else
    {
      Cap(mesh, _points[0], (_points[0] - _points[1]).normalized, inner, outer, clear);
      Cap(mesh, _points[_count - 1], (_points[_count - 1] - _points[_count - 2]).normalized, inner, outer, clear);
    }
  }

  protected void Line(VertexHelper mesh, float x, float y, float x2, float y2, float width = 2)
  {
    BeginPath();
    PathPoint(x, y);
    PathPoint(x2, y2);
    Stroke(mesh, width);
  }

  protected void Arc(VertexHelper mesh, float x, float y, float radius, float from, float to, float width = 2)
  {
    bool closed = Mathf.Abs(to - from) >= 359.9f;
    int segments = Mathf.Max(8, Mathf.CeilToInt(Mathf.Abs(to - from) / 10));
    BeginPath();
    for (int i = 0; i <= segments - (closed ? 1 : 0); i++)
    {
      float a = Mathf.Lerp(from, to, i / (float)segments) * Mathf.Deg2Rad;
      PathPoint(x + Mathf.Cos(a) * radius, y + Mathf.Sin(a) * radius);
    }
    Stroke(mesh, width, closed);
  }

  protected void Dot(VertexHelper mesh, float x, float y, float radius)
  {
    Rect rect = rectTransform.rect;
    float unit = Mathf.Min(rect.width, rect.height) / DesignSize;
    float feather = CoverageWidth();
    Vector2 center = rect.center + new Vector2(x - DesignSize * .5f, DesignSize * .5f - y) * unit;
    float inner = Mathf.Max(.05f, radius * unit - feather * .5f),
      outer = radius * unit + feather * .5f;
    Color clear = color;
    clear.a = 0;
    int start = mesh.currentVertCount;
    mesh.AddVert(center, color, Vector2.zero);
    for (int i = 0; i <= 20; i++)
    {
      float a = i * Mathf.PI / 10;
      Vector2 n = new(Mathf.Cos(a), Mathf.Sin(a));
      mesh.AddVert(center + n * inner, color, Vector2.zero);
      mesh.AddVert(center + n * outer, clear, Vector2.zero);
      if (i == 0)
        continue;
      int p = start + 1 + (i - 1) * 2,
        q = p + 2;
      mesh.AddTriangle(start, p, q);
      mesh.AddTriangle(p, p + 1, q + 1);
      mesh.AddTriangle(p, q + 1, q);
    }
  }

  private static void Connect(VertexHelper mesh, int a, int b)
  {
    for (int strip = 0; strip < 3; strip++)
    {
      mesh.AddTriangle(a + strip, b + strip, b + strip + 1);
      mesh.AddTriangle(a + strip, b + strip + 1, a + strip + 1);
    }
  }

  private float CoverageWidth()
  {
    // Screen-space camera canvases have a world-space scale determined by the
    // camera. Divide that out; only scale inside the canvas changes pixel size.
    if (canvas == null)
      return .8f;
    float canvasScale = Mathf.Max(.00001f, Mathf.Abs(canvas.transform.lossyScale.x));
    float localScale = Mathf.Abs(transform.lossyScale.x) / canvasScale;
    return .8f / Mathf.Max(.1f, canvas.scaleFactor * localScale);
  }

  private void Cap(VertexHelper mesh, Vector2 center, Vector2 direction, float inner, float outer, Color clear)
  {
    int start = mesh.currentVertCount;
    mesh.AddVert(center, color, Vector2.zero);
    Vector2 perpendicular = new(-direction.y, direction.x);
    for (int i = 0; i <= 8; i++)
    {
      float angle = Mathf.PI * (i / 8f - .5f);
      Vector2 n = direction * Mathf.Cos(angle) + perpendicular * Mathf.Sin(angle);
      mesh.AddVert(center + n * inner, color, Vector2.zero);
      mesh.AddVert(center + n * outer, clear, Vector2.zero);
      if (i == 0)
        continue;
      int a = start + 1 + (i - 1) * 2,
        b = a + 2;
      mesh.AddTriangle(start, a, b);
      mesh.AddTriangle(a, a + 1, b + 1);
      mesh.AddTriangle(a, b + 1, b);
    }
  }
}
