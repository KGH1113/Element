using System.Collections.Generic;
using Element.Configuration;
using Element.Domain.Elements;
using Element.Presentation.Overlay.Shared;
using UnityEngine;
using UnityEngine.UI;
using NVector2 = System.Numerics.Vector2;

namespace Element.Presentation.Overlay.Elements;

// A filled, optionally stroked shape. Canvas UI has no MSAA, so every edge
// gets a one-pixel fade. The mesh is rebuilt only when the shape, its colors
// or its size on screen change. A gradient layer reads its colors from a
// small texture (OverlayGradientTexture); solid layers keep theirs in the
// vertices.
[RequireComponent(typeof(CanvasRenderer))]
internal sealed class ShapeGraphic : MaskableGraphic
{
  private readonly List<NVector2> _corners = new(4);
  private readonly List<float> _radii = new(4);
  private readonly List<NVector2> _outline = new(128);
  private readonly List<Vector2> _local = new(128);
  private readonly List<Vector2> _normals = new(128);
  private readonly List<int> _triangles = new(384);
  private OverlayShapeSettings _shape;
  private readonly OverlayGradientTexture _gradients = new();
  private Color _fill,
    _stroke;
  private OverlayResolvedGradient _fillGradient,
    _strokeGradient;
  private OverlayGradientLayer _fillLayer,
    _strokeLayer;
  private bool _textured;
  private float _pixelsPerUnit = 1;

  public void SetShape(OverlayShapeSettings shape, Color fill, Color stroke) => SetShape(shape, fill, stroke, null, null);

  /// <summary>Null gradients are solid layers in <paramref name="fill"/> and <paramref name="stroke"/>.</summary>
  public void SetShape(
    OverlayShapeSettings shape,
    Color fill,
    Color stroke,
    OverlayResolvedGradient fillGradient,
    OverlayResolvedGradient strokeGradient
  )
  {
    _shape = shape;
    _fill = fill;
    _stroke = stroke;
    _fillGradient = fillGradient;
    _strokeGradient = strokeGradient;
    UpdateTexture();
    SetVerticesDirty();
  }

  // Edge fades are one screen pixel wide whatever the element's scale.
  public void SetPixelsPerUnit(float pixelsPerUnit)
  {
    if (pixelsPerUnit <= 0 || Mathf.Abs(pixelsPerUnit - _pixelsPerUnit) < .01f)
      return;
    _pixelsPerUnit = pixelsPerUnit;
    UpdateTexture();
    SetVerticesDirty();
  }

  public override Texture mainTexture => _textured && _gradients.Texture != null ? _gradients.Texture : s_WhiteTexture;

  private void UpdateTexture()
  {
    bool textured = _shape != null && (_fillGradient != null || _strokeGradient != null);
    if (!textured)
    {
      _fillLayer = _strokeLayer = default;
      if (_textured)
      {
        _textured = false;
        SetMaterialDirty();
      }
      return;
    }
    Texture before = _gradients.Texture;
    _gradients.Update(
      _fillGradient,
      _strokeGradient,
      _shape.Width,
      _shape.Height,
      _shape.Height * _pixelsPerUnit,
      out _fillLayer,
      out _strokeLayer
    );
    // A new texture goes to the renderer; new colors in the same one are already there.
    if (!_textured || before != _gradients.Texture)
    {
      _textured = true;
      SetMaterialDirty();
    }
  }

  protected override void OnDestroy()
  {
    _gradients.Dispose();
    base.OnDestroy();
  }

  protected override void OnPopulateMesh(VertexHelper mesh)
  {
    mesh.Clear();
    if (_shape == null)
      return;
    Rect rect = rectTransform.rect;
    float width = _shape.Width,
      height = _shape.Height;
    if (_shape.Kind == OverlayShapeKind.Circle)
      OverlayShapeGeometry.Ellipse(
        width,
        height,
        Mathf.Clamp(Mathf.CeilToInt((width + height) * _pixelsPerUnit * .25f), 24, 128),
        _outline
      );
    else
    {
      OverlayShapeGeometry.Corners(_shape, _corners);
      _radii.Clear();
      foreach (OverlayShapePoint point in _shape.Points)
        _radii.Add(point.Radius);
      OverlayShapeGeometry.Outline(_corners, _radii, Mathf.Max(.05f, _pixelsPerUnit * .35f), _outline);
    }
    RemoveDuplicates(_outline);
    if (_outline.Count < 3)
      return;
    // Box space has y down; the rect's local space has y up from its bottom left.
    _local.Clear();
    foreach (NVector2 point in _outline)
      _local.Add(new Vector2(rect.xMin + point.X, rect.yMax - point.Y));
    float area = 0;
    for (int i = 0, j = _local.Count - 1; i < _local.Count; j = i++)
      area += _local[j].x * _local[i].y - _local[i].x * _local[j].y;
    if (Mathf.Abs(area) < 1e-4f)
      return;
    ComputeNormals(area > 0);
    OverlayShapeGeometry.Triangulate(_outline, _triangles);
    float feather = 1f / Mathf.Max(.01f, _pixelsPerUnit);
    float stroke = Mathf.Min(_shape.StrokeWidth, Mathf.Min(width, height) * .5f);
    _box = rect;
    AddFill(mesh, feather);
    if (stroke > 0 && (_strokeLayer.Gradient || _stroke.a > 0))
      AddRing(mesh, 0, stroke, _strokeLayer.Gradient ? Color.white : _stroke, _strokeLayer, feather);
  }

  private Rect _box;

  // A vertex of a layer: its texture coordinate comes from where it sits in the box.
  private void Vert(VertexHelper mesh, Vector2 position, Color color, in OverlayGradientLayer layer) =>
    mesh.AddVert(
      position,
      color,
      layer.Uv(position.x - _box.xMin, _box.yMax - position.y, _shape.Width, _shape.Height)
    );

  private void AddFill(VertexHelper mesh, float feather)
  {
    bool gradient = _fillLayer.Gradient;
    if (!gradient && _fill.a <= 0)
      return;
    Color color = gradient ? Color.white : _fill;
    Color clear = color;
    clear.a = 0;
    int start = mesh.currentVertCount;
    int count = _local.Count;
    // Solid core pulled half a fade inward, then a ring fading outward.
    for (int i = 0; i < count; i++)
      Vert(mesh, _local[i] - _normals[i] * feather * .5f, color, _fillLayer);
    for (int i = 0; i < _triangles.Count; i += 3)
      mesh.AddTriangle(start + _triangles[i], start + _triangles[i + 1], start + _triangles[i + 2]);
    int outer = mesh.currentVertCount;
    for (int i = 0; i < count; i++)
      Vert(mesh, _local[i] + _normals[i] * feather * .5f, clear, _fillLayer);
    for (int i = 0; i < count; i++)
    {
      int next = (i + 1) % count;
      mesh.AddTriangle(start + i, outer + i, outer + next);
      mesh.AddTriangle(start + i, outer + next, start + next);
    }
  }

  // A band from `inset` to `inset + width` inside the outline, faded on both sides.
  private void AddRing(
    VertexHelper mesh,
    float inset,
    float width,
    Color color,
    in OverlayGradientLayer layer,
    float feather
  )
  {
    Color clear = color;
    clear.a = 0;
    int count = _local.Count;
    float[] offsets =
    {
      inset - feather * .5f,
      inset + feather * .5f,
      inset + width - feather * .5f,
      inset + width + feather * .5f,
    };
    if (offsets[2] < offsets[1])
      offsets[1] = offsets[2] = inset + width * .5f;
    int start = mesh.currentVertCount;
    for (int i = 0; i < count; i++)
    for (int band = 0; band < 4; band++)
      Vert(mesh, _local[i] - _normals[i] * offsets[band], band == 0 || band == 3 ? clear : color, layer);
    for (int i = 0; i < count; i++)
    {
      int a = start + i * 4,
        b = start + (i + 1) % count * 4;
      for (int band = 0; band < 3; band++)
      {
        mesh.AddTriangle(a + band, b + band, b + band + 1);
        mesh.AddTriangle(a + band, b + band + 1, a + band + 1);
      }
    }
  }

  // Outward unit normals, lengthened at corners so offsets keep a constant
  // distance from both edges, and capped so spikes stay short.
  private void ComputeNormals(bool counterClockwise)
  {
    _normals.Clear();
    int count = _local.Count;
    float side = counterClockwise ? 1 : -1;
    for (int i = 0; i < count; i++)
    {
      Vector2 previous = _local[(i + count - 1) % count],
        current = _local[i],
        next = _local[(i + 1) % count];
      Vector2 before = (current - previous).normalized,
        after = (next - current).normalized;
      Vector2 normalBefore = new Vector2(before.y, -before.x) * side;
      Vector2 normalAfter = new Vector2(after.y, -after.x) * side;
      Vector2 normal = (normalBefore + normalAfter).normalized;
      if (normal == Vector2.zero)
        normal = normalAfter;
      float miter = Vector2.Dot(normal, normalAfter);
      _normals.Add(normal / Mathf.Max(.25f, miter));
    }
  }

  private static void RemoveDuplicates(List<NVector2> points)
  {
    for (int i = points.Count - 1; i > 0 && points.Count > 0; i--)
      if (NVector2.DistanceSquared(points[i], points[i - 1]) < 1e-6f)
        points.RemoveAt(i);
    if (points.Count > 1 && NVector2.DistanceSquared(points[0], points[points.Count - 1]) < 1e-6f)
      points.RemoveAt(points.Count - 1);
  }

  public static Color Resolve(OverlayColorSettings color) => OverlayTextComposer.Resolve(color, OverlayTheme.Accent);
}
