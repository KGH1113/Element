using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ImplResourcePack.Presentation.Layout;

// One rounded, dashed perimeter. Rebuilt only when the selected target changes
// size, or its viewport scale changes; hidden targets don't submit any geometry.
[RequireComponent(typeof(CanvasRenderer))]
internal sealed class LayoutOutlineGraphic : MaskableGraphic
{
  private readonly List<Vector2> _path = new(40);
  private float _uiScale = 1;
  private Rect _exclusion;

  public void SetExclusion(Rect exclusion)
  {
    if (_exclusion == exclusion)
      return;
    _exclusion = exclusion;
    SetVerticesDirty();
  }

  public void SetScale(float scale)
  {
    if (_uiScale == scale)
      return;
    _uiScale = scale;
    SetVerticesDirty();
  }

  protected override void OnPopulateMesh(VertexHelper mesh)
  {
    mesh.Clear();
    Rect bounds = rectTransform.rect;
    float thickness = 3 * _uiScale;
    if (bounds.width <= thickness || bounds.height <= thickness)
      return;
    Rect r = new(
      bounds.xMin + thickness * .5f,
      bounds.yMin + thickness * .5f,
      bounds.width - thickness,
      bounds.height - thickness
    );
    float radius = Mathf.Min(8 * _uiScale, Mathf.Min(r.width, r.height) * .5f);
    _path.Clear();
    _path.Add(new Vector2(r.xMin + radius, r.yMax));
    Arc(new Vector2(r.xMax - radius, r.yMax - radius), radius, 90);
    Arc(new Vector2(r.xMax - radius, r.yMin + radius), radius, 0);
    Arc(new Vector2(r.xMin + radius, r.yMin + radius), radius, -90);
    Arc(new Vector2(r.xMin + radius, r.yMax - radius), radius, -180);
    _path.Add(_path[0]);
    float dash = 8 * _uiScale,
      gap = 5 * _uiScale,
      phase = 0;
    for (int i = 1; i < _path.Count; i++)
    {
      Vector2 a = _path[i - 1],
        b = _path[i];
      float length = Vector2.Distance(a, b),
        offset = 0;
      while (offset < length - .001f)
      {
        bool ink = phase < dash;
        float step = Mathf.Min(length - offset, (ink ? dash : dash + gap) - phase);
        if (step <= .001f)
        {
          phase = ink ? dash : 0;
          continue;
        }
        if (ink)
          ClippedStroke(
            mesh,
            Vector2.Lerp(a, b, offset / length),
            Vector2.Lerp(a, b, (offset + step) / length),
            thickness
          );
        offset += step;
        phase += step;
        if (phase >= dash + gap - .001f)
          phase = 0;
      }
    }
    Handle(mesh, new Vector2(bounds.xMin, bounds.yMin));
    Handle(mesh, new Vector2(bounds.xMax, bounds.yMin));
    Handle(mesh, new Vector2(bounds.xMin, bounds.yMax));
    Handle(mesh, new Vector2(bounds.xMax, bounds.yMax));
    Handle(mesh, new Vector2(bounds.center.x, bounds.yMin));
    Handle(mesh, new Vector2(bounds.center.x, bounds.yMax));
    Handle(mesh, new Vector2(bounds.xMin, bounds.center.y));
    Handle(mesh, new Vector2(bounds.xMax, bounds.center.y));
  }

  private void Arc(Vector2 center, float radius, float start)
  {
    for (int i = 0; i <= 8; i++)
    {
      float angle = (start - 90f * i / 8) * Mathf.Deg2Rad;
      _path.Add(center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius);
    }
  }

  private static void Stroke(VertexHelper mesh, Vector2 a, Vector2 b, float thickness)
  {
    Vector2 tangent = (b - a).normalized;
    Vector2 n = new(-tangent.y, tangent.x);
    int start = mesh.currentVertCount;
    float outer = thickness * .5f,
      inner = Mathf.Max(.1f, outer - .7f);
    for (int i = 0; i < 4; i++)
    {
      float offset =
        i == 0 ? outer
        : i == 1 ? inner
        : i == 2 ? -inner
        : -outer;
      Color c = i == 0 || i == 3 ? new Color(1, 1, 1, 0) : Color.white;
      mesh.AddVert(a + n * offset, c, Vector2.zero);
      mesh.AddVert(b + n * offset, c, Vector2.zero);
      if (i == 0)
        continue;
      int p = start + (i - 1) * 2;
      mesh.AddTriangle(p, p + 1, p + 3);
      mesh.AddTriangle(p, p + 3, p + 2);
    }
  }

  private void Handle(VertexHelper mesh, Vector2 center)
  {
    Rect safe = _exclusion;
    safe.xMin -= 5 * _uiScale;
    safe.yMin -= 5 * _uiScale;
    safe.xMax += 5 * _uiScale;
    safe.yMax += 5 * _uiScale;
    if (safe.Contains(center))
      return;
    Circle(mesh, center, 4.5f * _uiScale, new Color(.06f, .06f, .06f));
    Circle(mesh, center, 3.25f * _uiScale, Color.white);
  }

  private void ClippedStroke(VertexHelper mesh, Vector2 a, Vector2 b, float thickness)
  {
    // Exclude the whole stroke width, including antialiasing, even at rounded
    // toolbar corners. No line can peek through the foreground panel.
    Rect r = _exclusion;
    float half = thickness * .5f + 1;
    r.xMin -= half;
    r.xMax += half;
    r.yMin -= half;
    r.yMax += half;
    Vector2 d = b - a;
    float enter = 0,
      leave = 1;
    if (
      !ClipAxis(a.x, d.x, r.xMin, r.xMax, ref enter, ref leave)
      || !ClipAxis(a.y, d.y, r.yMin, r.yMax, ref enter, ref leave)
    )
    {
      Stroke(mesh, a, b, thickness);
      return;
    }
    if (enter > .001f)
      Stroke(mesh, a, a + d * enter, thickness);
    if (leave < .999f)
      Stroke(mesh, a + d * leave, b, thickness);
  }

  private static bool ClipAxis(float start, float delta, float min, float max, ref float enter, ref float leave)
  {
    if (Mathf.Abs(delta) < .0001f)
      return start >= min && start <= max;
    float a = (min - start) / delta,
      b = (max - start) / delta;
    enter = Mathf.Max(enter, Mathf.Min(a, b));
    leave = Mathf.Min(leave, Mathf.Max(a, b));
    return enter <= leave;
  }

  private static void Circle(VertexHelper mesh, Vector2 center, float radius, Color color)
  {
    int start = mesh.currentVertCount;
    mesh.AddVert(center, color, Vector2.zero);
    for (int i = 0; i <= 20; i++)
    {
      float angle = i * Mathf.PI * 2 / 20;
      mesh.AddVert(center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius, color, Vector2.zero);
      if (i > 0)
        mesh.AddTriangle(start, start + i, start + i + 1);
    }
  }
}
