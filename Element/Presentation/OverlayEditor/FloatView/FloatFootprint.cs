using UnityEngine;

namespace Element.Presentation.OverlayEditor.FloatView;

/// <summary>The face's four projected corners in screen pixels measured from the top left.</summary>
internal sealed class FloatFootprint
{
  public readonly Vector2[] Corners = new Vector2[4];
  public Rect Bounds { get; private set; }

  public void Project(RectTransform face, Matrix4x4 worldToScreen)
  {
    Rect r = face.rect;
    Matrix4x4 projection = worldToScreen * face.localToWorldMatrix;
    Vector2 min = new(float.PositiveInfinity, float.PositiveInfinity);
    Vector2 max = new(float.NegativeInfinity, float.NegativeInfinity);
    for (int i = 0; i < Corners.Length; i++)
    {
      Vector3 local = new(i < 2 ? r.xMin : r.xMax, i == 1 || i == 2 ? r.yMax : r.yMin);
      Vector3 point = projection.MultiplyPoint3x4(local);
      Corners[i] = new Vector2(point.x, -point.y);
      min = Vector2.Min(min, Corners[i]);
      max = Vector2.Max(max, Corners[i]);
    }
    Bounds = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
  }

  public float DistanceSquared(Vector2 pointer, float radius)
  {
    if (
      pointer.x < Bounds.xMin - radius
      || pointer.x > Bounds.xMax + radius
      || pointer.y < Bounds.yMin - radius
      || pointer.y > Bounds.yMax + radius
    )
      return float.PositiveInfinity;
    bool inside = true;
    float nearest = float.PositiveInfinity;
    for (int i = 0; i < Corners.Length; i++)
    {
      Vector2 edge = Corners[(i + 1) % 4] - Corners[i];
      Vector2 delta = pointer - Corners[i];
      inside &= edge.x * delta.y - edge.y * delta.x >= 0;
      float along = Mathf.Clamp01(Vector2.Dot(delta, edge) / Mathf.Max(edge.sqrMagnitude, 1e-10f));
      nearest = Mathf.Min(nearest, (delta - edge * along).sqrMagnitude);
    }
    return inside && Bounds.width > 0 && Bounds.height > 0 ? 0 : nearest;
  }
}
