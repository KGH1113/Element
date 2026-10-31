using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Element.Presentation.Layout;

// The selected shape's handles: squares on its corners and small circles
// inside them for rounding. Positions are screen pixels from the top left.
[RequireComponent(typeof(CanvasRenderer))]
internal sealed class ShapeHandlesGraphic : MaskableGraphic
{
  private static readonly Color Ink = new(.08f, .08f, .08f, .9f);
  private readonly List<Vector2> _corners = new(4);
  private readonly List<Vector2> _radii = new(4);
  private float _scale = 1;
  private int _active = -1;
  private bool _activeRadius;

  public void Set(List<Vector2> corners, List<Vector2> radii, float scale, int active, bool activeRadius)
  {
    if (
      SameList(_corners, corners)
      && SameList(_radii, radii)
      && _scale == scale
      && _active == active
      && _activeRadius == activeRadius
    )
      return;
    _corners.Clear();
    _corners.AddRange(corners);
    _radii.Clear();
    _radii.AddRange(radii);
    _scale = scale;
    _active = active;
    _activeRadius = activeRadius;
    SetVerticesDirty();
  }

  protected override void OnPopulateMesh(VertexHelper mesh)
  {
    mesh.Clear();
    for (int i = 0; i < _radii.Count; i++)
    {
      if (float.IsNaN(_radii[i].x))
        continue;
      Vector2 p = Local(_radii[i]);
      bool active = _activeRadius && i == _active;
      Disc(mesh, p, 5.5f * _scale, Ink);
      Disc(mesh, p, 4f * _scale, active ? color : Color.white);
    }
    for (int i = 0; i < _corners.Count; i++)
    {
      Vector2 p = Local(_corners[i]);
      bool active = !_activeRadius && i == _active;
      Square(mesh, p, 6.5f * _scale, Ink);
      Square(mesh, p, 5f * _scale, active ? color : Color.white);
    }
  }

  private Vector2 Local(Vector2 screen)
  {
    Rect rect = rectTransform.rect;
    return new Vector2(rect.xMin + screen.x, rect.yMax - screen.y);
  }

  private static void Square(VertexHelper mesh, Vector2 center, float half, Color fill)
  {
    int start = mesh.currentVertCount;
    mesh.AddVert(center + new Vector2(-half, -half), fill, Vector2.zero);
    mesh.AddVert(center + new Vector2(-half, half), fill, Vector2.zero);
    mesh.AddVert(center + new Vector2(half, half), fill, Vector2.zero);
    mesh.AddVert(center + new Vector2(half, -half), fill, Vector2.zero);
    mesh.AddTriangle(start, start + 1, start + 2);
    mesh.AddTriangle(start, start + 2, start + 3);
  }

  private static void Disc(VertexHelper mesh, Vector2 center, float radius, Color fill)
  {
    Color clear = fill;
    clear.a = 0;
    int start = mesh.currentVertCount;
    mesh.AddVert(center, fill, Vector2.zero);
    const int segments = 20;
    for (int i = 0; i <= segments; i++)
    {
      float angle = i * Mathf.PI * 2 / segments;
      Vector2 direction = new(Mathf.Cos(angle), Mathf.Sin(angle));
      mesh.AddVert(center + direction * (radius - .5f), fill, Vector2.zero);
      mesh.AddVert(center + direction * (radius + .5f), clear, Vector2.zero);
      if (i == 0)
        continue;
      int p = start + 1 + (i - 1) * 2,
        q = p + 2;
      mesh.AddTriangle(start, p, q);
      mesh.AddTriangle(p, p + 1, q + 1);
      mesh.AddTriangle(p, q + 1, q);
    }
  }

  private static bool SameList(List<Vector2> a, List<Vector2> b)
  {
    if (a.Count != b.Count)
      return false;
    for (int i = 0; i < a.Count; i++)
      if (float.IsNaN(a[i].x) != float.IsNaN(b[i].x) || (a[i] - b[i]).sqrMagnitude > .01f)
        return false;
    return true;
  }
}
