using System;
using System.Collections.Generic;
using Element.Domain.Elements;
using Element.Presentation.Overlay.Elements;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Element.Presentation.Settings;

/// <summary>
/// A gradient drawn into its rect over a checkerboard, for the color bar, the
/// kind tiles and swatches. Colors sit on a grid of vertices; along the color
/// bar the grid also has a column at every stop, so the bar is exact.
/// </summary>
[RequireComponent(typeof(CanvasRenderer))]
internal sealed class SettingsGradientPreview : MaskableGraphic
{
  private static readonly Color CheckerDark = new(.30f, .30f, .30f),
    CheckerLight = new(.48f, .48f, .48f);

  private readonly OverlayResolvedGradient _gradient = new();
  private readonly List<float> _columns = new(80);
  private bool _solid = true;
  private Color _color = Color.white;

  /// <summary>Runs left to right with stops <see cref="Inset"/> in from each end, like the color bar.</summary>
  public bool AlongX;

  public float Inset;
  public int Columns = 16,
    Rows = 10;
  public float Cell = 5;

  public void SetGradient(OverlayResolvedGradient gradient)
  {
    if (gradient == null)
      return;
    if (!_solid && _gradient.SameAs(gradient))
      return;
    _gradient.CopyFrom(gradient);
    _solid = false;
    SetVerticesDirty();
  }

  public void SetSolid(Color color)
  {
    if (_solid && _color == color)
      return;
    _solid = true;
    _color = color;
    SetVerticesDirty();
  }

  protected override void OnPopulateMesh(VertexHelper mesh)
  {
    mesh.Clear();
    Rect rect = rectTransform.rect;
    if (rect.width <= 0 || rect.height <= 0)
      return;
    Checker(mesh, rect);
    if (_solid)
    {
      Quad(mesh, rect.xMin, rect.yMin, rect.xMax, rect.yMax, _color, _color, _color, _color);
      return;
    }
    if (AlongX)
    {
      DrawAlongX(mesh, rect);
      return;
    }
    OverlayGradientFrame frame = _gradient.Frame(rect.width, rect.height);
    int columns = Mathf.Max(1, Columns),
      rows = Mathf.Max(1, Rows);
    int start = mesh.currentVertCount;
    for (int row = 0; row <= rows; row++)
    for (int column = 0; column <= columns; column++)
    {
      float x = rect.width * column / columns,
        y = rect.height * row / rows;
      mesh.AddVert(new Vector2(rect.xMin + x, rect.yMax - y), ToColor(_gradient.Sample(frame.At(x, y))), Vector2.zero);
    }
    for (int row = 0; row < rows; row++)
    for (int column = 0; column < columns; column++)
    {
      int a = start + row * (columns + 1) + column;
      int b = a + columns + 1;
      mesh.AddTriangle(a, a + 1, b + 1);
      mesh.AddTriangle(a, b + 1, b);
    }
  }

  private void DrawAlongX(VertexHelper mesh, Rect rect)
  {
    float span = Mathf.Max(1, rect.width - Inset * 2);
    _columns.Clear();
    _columns.Add(0);
    _columns.Add(rect.width);
    for (int i = 0; i <= 48; i++)
      _columns.Add(Inset + span * i / 48);
    foreach (OverlayGradientSample stop in _gradient.Stops)
      _columns.Add(Inset + span * stop.Position);
    _columns.Sort();
    for (int i = 0; i < _columns.Count - 1; i++)
    {
      float left = _columns[i],
        right = _columns[i + 1];
      if (right - left < 1e-3f)
        continue;
      // A hard edge between two stops at one place: each side takes its own stop.
      Color from = ToColor(_gradient.Sample((left - Inset) / span + 1e-5f)),
        to = ToColor(_gradient.Sample((right - Inset) / span - 1e-5f));
      Quad(mesh, rect.xMin + left, rect.yMin, rect.xMin + right, rect.yMax, from, from, to, to);
    }
  }

  private void Checker(VertexHelper mesh, Rect rect)
  {
    int columns = Mathf.CeilToInt(rect.width / Cell),
      rows = Mathf.CeilToInt(rect.height / Cell);
    for (int y = 0; y < rows; y++)
    for (int x = 0; x < columns; x++)
    {
      Color color = (x + y) % 2 == 0 ? CheckerDark : CheckerLight;
      Quad(
        mesh,
        rect.xMin + x * Cell,
        rect.yMin + y * Cell,
        Mathf.Min(rect.xMax, rect.xMin + (x + 1) * Cell),
        Mathf.Min(rect.yMax, rect.yMin + (y + 1) * Cell),
        color,
        color,
        color,
        color
      );
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

  internal static Color ToColor(OverlayRgba color) => new(color.R, color.G, color.B, color.A);
}

/// <summary>
/// A color square: a rounded edge (white, or the accent when picked) around
/// the color over a checkerboard, so see-through colors read as such. With a
/// notch it points at where its color sits on a gradient line.
/// </summary>
[RequireComponent(typeof(CanvasRenderer))]
internal sealed class SettingsSwatchGraphic : MaskableGraphic
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

  public void Set(Color fill, Color edge)
  {
    if (_fill == fill && _edge == edge)
      return;
    _fill = fill;
    _edge = edge;
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
    RoundedRect(mesh, rect, Radius + .8f, -.8f, Shadow);
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

/// <summary>An invisible area that reports presses, drags and hovering, for handles and the color bar.</summary>
[RequireComponent(typeof(CanvasRenderer))]
internal sealed class SettingsPointerArea
  : MaskableGraphic,
    IPointerDownHandler,
    IPointerUpHandler,
    IInitializePotentialDragHandler,
    IDragHandler,
    IPointerMoveHandler,
    IPointerExitHandler
{
  public Action<PointerEventData> Down,
    Dragged,
    Up,
    Moved;
  public Action Exited;

  public void OnPointerMove(PointerEventData data) => Moved?.Invoke(data);

  public void OnPointerExit(PointerEventData data) => Exited?.Invoke();

  protected override void OnPopulateMesh(VertexHelper mesh) => mesh.Clear();

  public void OnPointerDown(PointerEventData data)
  {
    if (data.button == PointerEventData.InputButton.Left)
      Down?.Invoke(data);
  }

  // Handles follow the pointer from the first pixel.
  public void OnInitializePotentialDrag(PointerEventData data) => data.useDragThreshold = false;

  public void OnDrag(PointerEventData data)
  {
    if (data.button == PointerEventData.InputButton.Left)
      Dragged?.Invoke(data);
  }

  public void OnPointerUp(PointerEventData data)
  {
    if (data.button == PointerEventData.InputButton.Left)
      Up?.Invoke(data);
  }
}

/// <summary>The small icons of the gradient controls.</summary>
internal enum SettingsGradientIconKind
{
  Flip,
  Rotate,
  Delete,
}

internal sealed class SettingsGradientIcon : SettingsVectorGraphic
{
  public SettingsGradientIconKind Kind;

  protected override void OnPopulateMesh(VertexHelper mesh)
  {
    mesh.Clear();
    switch (Kind)
    {
      case SettingsGradientIconKind.Flip:
        // Two arrows passing each other.
        Line(mesh, 4, 8.5f, 19, 8.5f);
        Line(mesh, 15.5f, 5, 19, 8.5f);
        Line(mesh, 15.5f, 12, 19, 8.5f);
        Line(mesh, 20, 15.5f, 5, 15.5f);
        Line(mesh, 8.5f, 12, 5, 15.5f);
        Line(mesh, 8.5f, 19, 5, 15.5f);
        break;
      case SettingsGradientIconKind.Rotate:
        // Most of a circle with an arrow on its end.
        Arc(mesh, 12, 12.5f, 7, -60, 220);
        Line(mesh, 15.5f, 6.4f, 19.6f, 6.4f);
        Line(mesh, 19.6f, 2.3f, 19.6f, 6.4f);
        break;
      default:
        // A bin with its lid.
        Line(mesh, 4.5f, 7, 19.5f, 7);
        Line(mesh, 9.5f, 7, 9.5f, 4.5f);
        Line(mesh, 9.5f, 4.5f, 14.5f, 4.5f);
        Line(mesh, 14.5f, 4.5f, 14.5f, 7);
        Line(mesh, 6.5f, 7, 7.5f, 19.5f);
        Line(mesh, 7.5f, 19.5f, 16.5f, 19.5f);
        Line(mesh, 16.5f, 19.5f, 17.5f, 7);
        break;
    }
  }
}
