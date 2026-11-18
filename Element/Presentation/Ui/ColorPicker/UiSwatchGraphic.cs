using UnityEngine;
using UnityEngine.UI;

namespace Element.Presentation.Ui.ColorPicker;

/// <summary>
/// A color square: a rounded edge (white, or the accent when picked) around
/// the color over a checkerboard, so see-through colors read as such. With a
/// notch it points at where its color sits on a gradient line.
/// </summary>
[RequireComponent(typeof(CanvasRenderer))]
internal sealed class UiSwatchGraphic : MaskableGraphic
{
  private static readonly Color CheckerDark = new(.42f, .42f, .42f),
    CheckerLight = new(.80f, .80f, .80f),
    Shadow = new(0, 0, 0, .5f);

  private Color _edge = Color.white,
    _fill = Color.white;
  private Vector2 _notch;
  private float _reach;

  public float Radius = 4.5f;
  public float Ring = 2.5f;

  public void Set(Color fill, Color edge, float ring)
  {
    if (_fill == fill && _edge == edge && Mathf.Approximately(Ring, ring))
      return;
    _fill = fill;
    _edge = edge;
    Ring = ring;
    SetVerticesDirty();
  }

  /// <summary>A notch toward <paramref name="direction"/> (local space), its tip <paramref name="reach"/> from the center; zero for none.</summary>
  public void SetNotch(Vector2 direction, float reach)
  {
    if (_notch == direction && Mathf.Approximately(_reach, reach))
      return;
    _notch = direction;
    _reach = reach;
    SetVerticesDirty();
  }

  protected override void OnPopulateMesh(VertexHelper mesh)
  {
    mesh.Clear();
    Rect rect = rectTransform.rect;
    if (rect.width <= 0 || rect.height <= 0)
      return;
    if (_reach > 0 && _notch != Vector2.zero)
    {
      Vector2 direction = _notch.normalized,
        side = new(-direction.y, direction.x);
      Vector2 tip = rect.center + direction * _reach,
        bottom = rect.center + direction * (_reach - 8);
      int notch = mesh.currentVertCount;
      mesh.AddVert(tip + direction * .8f, Shadow, Vector2.zero);
      mesh.AddVert(bottom + side * 5.8f, Shadow, Vector2.zero);
      mesh.AddVert(bottom - side * 5.8f, Shadow, Vector2.zero);
      mesh.AddTriangle(notch, notch + 1, notch + 2);
      notch = mesh.currentVertCount;
      mesh.AddVert(tip, _edge, Vector2.zero);
      mesh.AddVert(bottom + side * 5, _edge, Vector2.zero);
      mesh.AddVert(bottom - side * 5, _edge, Vector2.zero);
      mesh.AddTriangle(notch, notch + 1, notch + 2);
    }
    RoundedRect(mesh, rect, Radius, .8f, Shadow);
    RoundedRect(mesh, rect, Radius, 0, _edge);
    // The color over a checkerboard, inside the edge.
    Rect inner = new(rect.x + Ring, rect.y + Ring, rect.width - Ring * 2, rect.height - Ring * 2);
    const int cells = 2;
    for (int y = 0; y < cells; y++)
    for (int x = 0; x < cells; x++)
    {
      Color checker = (x + y) % 2 == 0 ? CheckerDark : CheckerLight;
      Color color = Color.Lerp(checker, new Color(_fill.r, _fill.g, _fill.b, 1), _fill.a);
      float left = inner.xMin + inner.width * x / cells,
        right = inner.xMin + inner.width * (x + 1) / cells,
        bottom = inner.yMin + inner.height * y / cells,
        top = inner.yMin + inner.height * (y + 1) / cells;
      int start = mesh.currentVertCount;
      mesh.AddVert(new Vector2(left, bottom), color, Vector2.zero);
      mesh.AddVert(new Vector2(left, top), color, Vector2.zero);
      mesh.AddVert(new Vector2(right, top), color, Vector2.zero);
      mesh.AddVert(new Vector2(right, bottom), color, Vector2.zero);
      mesh.AddTriangle(start, start + 1, start + 2);
      mesh.AddTriangle(start, start + 2, start + 3);
    }
  }

  // A filled rounded rect grown by `grow` (negative shrinks), with a soft edge.
  private static void RoundedRect(VertexHelper mesh, Rect rect, float radius, float grow, Color color)
  {
    rect = new Rect(rect.x - grow, rect.y - grow, rect.width + grow * 2, rect.height + grow * 2);
    radius = Mathf.Clamp(radius + grow, 0, Mathf.Min(rect.width, rect.height) * .5f);
    Color clear = color;
    clear.a = 0;
    const int segments = 4;
    int start = mesh.currentVertCount;
    mesh.AddVert(rect.center, color, Vector2.zero);
    int count = 0;
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
        mesh.AddVert(center + direction * (radius - .4f), color, Vector2.zero);
        mesh.AddVert(center + direction * (radius + .4f), clear, Vector2.zero);
        count++;
      }
    }
    for (int i = 0; i < count; i++)
    {
      int current = start + 1 + i * 2,
        next = start + 1 + (i + 1) % count * 2;
      mesh.AddTriangle(start, current, next);
      mesh.AddTriangle(current, current + 1, next + 1);
      mesh.AddTriangle(current, next + 1, next);
    }
  }
}
