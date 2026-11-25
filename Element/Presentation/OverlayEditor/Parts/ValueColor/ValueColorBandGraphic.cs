using System.Collections.Generic;
using Element.Configuration.Overlay;
using Element.Domain.Overlay.Gradient;
using Element.Domain.Overlay.ValueColors;
using Element.Presentation.Ui.Graphics;
using UnityEngine;
using UnityEngine.UI;

namespace Element.Presentation.OverlayEditor.Parts.ValueColor;

/// <summary>
/// A color that follows a value, drawn as a rounded band: values run left
/// to right, the lowest stop <c>Inset</c> in from the left end and the
/// highest as far in from the right. Smoothly it blends from stop to stop;
/// in steps it holds each stop's color up to the next, with a sharp edge.
/// An opaque band under the colors keeps what is behind from showing through
/// a see-through stop. Its edges are smooth at any scale.
/// </summary>
[RequireComponent(typeof(CanvasRenderer))]
internal sealed class ValueColorBandGraphic : MaskableGraphic
{
  private const int Columns = 56;

  private readonly List<float> _columns = new(Columns + OverlayValueColor.MaximumStops + 4);
  private OverlayValueColorRamp _ramp;
  private OverlayRgba _highlight;
  private Color _under;
  private float _low,
    _high = 1,
    _inset;

  /// <summary>
  /// Draws <paramref name="ramp"/> from <paramref name="low"/> at
  /// <paramref name="inset"/> in from the left end to <paramref name="high"/>
  /// as far in from the right, over <paramref name="under"/>.
  /// </summary>
  public void Show(OverlayValueColorRamp ramp, Color highlight, float low, float high, float inset, Color under)
  {
    _ramp = ramp;
    _highlight = new OverlayRgba(highlight.r, highlight.g, highlight.b, 1);
    _low = low;
    _high = high;
    _inset = inset;
    _under = under;
    SetVerticesDirty();
  }

  /// <summary>The color drawn at <paramref name="x"/> from the band's left end.</summary>
  public Color ColorAt(float x)
  {
    if (_ramp == null || _ramp.Count == 0)
      return _under;
    float width = Mathf.Max(1, rectTransform.rect.width - _inset * 2);
    double value = _low + (x - _inset) / width * (_high - _low);
    OverlayRgba color = _ramp.Evaluate(value, _highlight);
    return new Color(color.R, color.G, color.B, color.A);
  }

  protected override void OnPopulateMesh(VertexHelper mesh)
  {
    mesh.Clear();
    Rect rect = rectTransform.rect;
    if (rect.width <= rect.height || rect.height <= 0)
      return;
    float feather = UiMeshShapes.Feather(this);
    float radius = rect.height * .5f;
    float left = radius,
      right = rect.width - radius;
    _columns.Clear();
    _columns.Add(left);
    _columns.Add(right);
    for (int i = 1; i < Columns; i++)
      _columns.Add(left + (right - left) * i / Columns);
    // Each stop is a column, so a step's edge is exact.
    float span = Mathf.Max(1e-6f, _high - _low),
      width = rect.width - _inset * 2;
    if (_ramp != null)
      for (int i = 0; i < _ramp.Count; i++)
      {
        float x = _inset + (_ramp.At(i) - _low) / span * width;
        if (x > left && x < right)
          _columns.Add(x);
      }
    _columns.Sort();
    Strip(mesh, rect, feather, true);
    Strip(mesh, rect, feather, false);
  }

  // The rounded band in the colors (or the opaque band under them).
  private void Strip(VertexHelper mesh, Rect rect, float feather, bool under)
  {
    float radius = rect.height * .5f,
      middle = rect.center.y;
    float half = feather * .5f;
    float solidTop = rect.yMax - half,
      solidBottom = rect.yMin + half,
      edgeTop = rect.yMax + half,
      edgeBottom = rect.yMin - half;
    Color first = under ? _under : ColorAt(_columns[0] + 1e-3f),
      last = under ? _under : ColorAt(_columns[_columns.Count - 1] - 1e-3f);
    Cap(mesh, new Vector2(rect.xMin + radius, middle), radius, first, feather, Mathf.PI * .5f);
    Cap(mesh, new Vector2(rect.xMax - radius, middle), radius, last, feather, -Mathf.PI * .5f);
    for (int i = 0; i < _columns.Count - 1; i++)
    {
      float from = _columns[i],
        to = _columns[i + 1];
      if (to - from < 1e-3f)
        continue;
      Color a = under ? _under : ColorAt(from + 1e-3f),
        b = under ? _under : ColorAt(to - 1e-3f);
      float x0 = rect.xMin + from,
        x1 = rect.xMin + to;
      Quad(mesh, x0, solidBottom, x1, solidTop, a, a, b, b);
      Quad(mesh, x0, solidTop, x1, edgeTop, a, Clear(a), Clear(b), b);
      Quad(mesh, x0, edgeBottom, x1, solidBottom, Clear(a), a, b, Clear(b));
    }
  }

  // A half circle facing away from the band's middle, from angle start.
  private static void Cap(VertexHelper mesh, Vector2 center, float radius, Color color, float feather, float start)
  {
    const int segments = 20;
    Color clear = Clear(color);
    float inner = Mathf.Max(0, radius - feather * .5f),
      outer = radius + feather * .5f;
    int first = mesh.currentVertCount;
    mesh.AddVert(center, color, Vector2.zero);
    for (int i = 0; i <= segments; i++)
    {
      float angle = start + Mathf.PI * i / segments;
      Vector2 direction = new(Mathf.Cos(angle), Mathf.Sin(angle));
      mesh.AddVert(center + direction * inner, color, Vector2.zero);
      mesh.AddVert(center + direction * outer, clear, Vector2.zero);
    }
    for (int i = 0; i < segments; i++)
    {
      int current = first + 1 + i * 2,
        next = current + 2;
      mesh.AddTriangle(first, current, next);
      mesh.AddTriangle(current, current + 1, next + 1);
      mesh.AddTriangle(current, next + 1, next);
    }
  }

  // Corners bottom left, top left, top right, bottom right.
  private static void Quad(
    VertexHelper mesh,
    float left,
    float bottom,
    float right,
    float top,
    Color bottomLeft,
    Color topLeft,
    Color topRight,
    Color bottomRight
  )
  {
    int start = mesh.currentVertCount;
    mesh.AddVert(new Vector2(left, bottom), bottomLeft, Vector2.zero);
    mesh.AddVert(new Vector2(left, top), topLeft, Vector2.zero);
    mesh.AddVert(new Vector2(right, top), topRight, Vector2.zero);
    mesh.AddVert(new Vector2(right, bottom), bottomRight, Vector2.zero);
    mesh.AddTriangle(start, start + 1, start + 2);
    mesh.AddTriangle(start, start + 2, start + 3);
  }

  private static Color Clear(Color color)
  {
    color.a = 0;
    return color;
  }
}
