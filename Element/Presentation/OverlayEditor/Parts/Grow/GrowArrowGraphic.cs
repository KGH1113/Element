using Element.Presentation.Ui.Graphics;
using UnityEngine;
using UnityEngine.UI;

namespace Element.Presentation.OverlayEditor.Parts.Grow;

/// <summary>A short arrow from the middle of its square toward a neighbouring point.</summary>
[RequireComponent(typeof(CanvasRenderer))]
internal sealed class GrowArrowGraphic : UiVectorGraphic
{
  private Vector2 _direction = Vector2.right;

  /// <summary><paramref name="direction"/> is right and down positive, as on screen.</summary>
  public void Point(Vector2 direction)
  {
    direction = direction.normalized;
    if (direction == _direction)
      return;
    _direction = direction;
    SetVerticesDirty();
  }

  protected override void OnPopulateMesh(VertexHelper mesh)
  {
    mesh.Clear();
    Vector2 d = _direction;
    Vector2 tail = new Vector2(12, 12) - d * 6,
      tip = new Vector2(12, 12) + d * 6;
    Line(mesh, tail.x, tail.y, tip.x, tip.y, 1.8f);
    // Two barbs, each 40° off the shaft.
    Vector2 back = -d * 4.5f;
    Vector2 left = Rotate(back, 40),
      right = Rotate(back, -40);
    Line(mesh, tip.x, tip.y, tip.x + left.x, tip.y + left.y, 1.8f);
    Line(mesh, tip.x, tip.y, tip.x + right.x, tip.y + right.y, 1.8f);
  }

  private static Vector2 Rotate(Vector2 v, float degrees)
  {
    float r = degrees * Mathf.Deg2Rad;
    float c = Mathf.Cos(r),
      s = Mathf.Sin(r);
    return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
  }
}
