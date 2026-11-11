using System.Collections.Generic;
using Element.Domain.Overlay.Gradient;
using Element.Presentation.Overlay.Elements;
using UnityEngine;
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
