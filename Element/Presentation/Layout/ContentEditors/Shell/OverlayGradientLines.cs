using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Element.Presentation.Layout.ContentEditors.Shell;

/// <summary>Thin lines and dots in frame units (y down), for the gradient handles.</summary>
[RequireComponent(typeof(CanvasRenderer))]
internal sealed class OverlayGradientLines : MaskableGraphic
{
  private readonly List<(Vector2 A, Vector2 B, Color Color, float Width)> _segments = new();
  private readonly List<(Vector2 At, float Radius, Color Color)> _dots = new();
  private Rect _rect;

  public void Begin(Rect rect)
  {
    _rect = rect;
    _segments.Clear();
    _dots.Clear();
  }

  public void Segment(Vector2 a, Vector2 b, Color color, float width)
  {
    // A dark edge under each white line keeps it readable on light colors.
    _segments.Add((a, b, new Color(0, 0, 0, color.a * .5f), width + 2));
    _segments.Add((a, b, color, width));
  }

  /// <summary>A closed outline; dashed in short pieces.</summary>
  public void Outline(List<Vector2> points, Color color, float width, bool dashed)
  {
    int count = points.Count;
    for (int i = 0; i < count; i++)
    {
      Vector2 a = points[i],
        b = points[(i + 1) % count];
      if (!dashed)
      {
        Segment(a, b, color, width);
        continue;
      }
      float length = (b - a).magnitude;
      for (float t = 0; t < length; t += 10)
        Segment(Vector2.Lerp(a, b, t / length), Vector2.Lerp(a, b, Mathf.Min(1, (t + 5) / length)), color, width);
    }
  }

  public void Dot(Vector2 at, float radius, Color color)
  {
    _dots.Add((at, radius + 1, new Color(0, 0, 0, .5f)));
    _dots.Add((at, radius, color));
  }

  public void End() => SetVerticesDirty();

  private Vector2 Local(Vector2 frame) => new(_rect.xMin + frame.x, _rect.yMax - frame.y);

  protected override void OnPopulateMesh(VertexHelper mesh)
  {
    mesh.Clear();
    foreach ((Vector2 a, Vector2 b, Color color, float width) in _segments)
    {
      Vector2 from = Local(a),
        to = Local(b);
      Vector2 along = to - from;
      if (along.sqrMagnitude < 1e-6f)
        continue;
      Vector2 normal = new Vector2(-along.y, along.x).normalized;
      float inner = Mathf.Max(.05f, width * .5f - .4f),
        outer = width * .5f + .4f;
      Color clear = color;
      clear.a = 0;
      int start = mesh.currentVertCount;
      mesh.AddVert(from + normal * outer, clear, Vector2.zero);
      mesh.AddVert(from + normal * inner, color, Vector2.zero);
      mesh.AddVert(from - normal * inner, color, Vector2.zero);
      mesh.AddVert(from - normal * outer, clear, Vector2.zero);
      mesh.AddVert(to + normal * outer, clear, Vector2.zero);
      mesh.AddVert(to + normal * inner, color, Vector2.zero);
      mesh.AddVert(to - normal * inner, color, Vector2.zero);
      mesh.AddVert(to - normal * outer, clear, Vector2.zero);
      for (int strip = 0; strip < 3; strip++)
      {
        mesh.AddTriangle(start + strip, start + 4 + strip, start + 5 + strip);
        mesh.AddTriangle(start + strip, start + 5 + strip, start + 1 + strip);
      }
    }
    foreach ((Vector2 at, float radius, Color color) in _dots)
    {
      Vector2 center = Local(at);
      Color clear = color;
      clear.a = 0;
      int start = mesh.currentVertCount;
      mesh.AddVert(center, color, Vector2.zero);
      const int segments = 16;
      for (int i = 0; i <= segments; i++)
      {
        float angle = i * Mathf.PI * 2 / segments;
        Vector2 n = new(Mathf.Cos(angle), Mathf.Sin(angle));
        mesh.AddVert(center + n * (radius - .4f), color, Vector2.zero);
        mesh.AddVert(center + n * (radius + .4f), clear, Vector2.zero);
        if (i == 0)
          continue;
        int p = start + 1 + (i - 1) * 2,
          q = p + 2;
        mesh.AddTriangle(start, p, q);
        mesh.AddTriangle(p, p + 1, q + 1);
        mesh.AddTriangle(p, q + 1, q);
      }
    }
  }
}
