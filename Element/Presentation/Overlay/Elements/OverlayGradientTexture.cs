using System;
using Element.Configuration;
using Element.Domain.Elements;
using Element.Presentation.Settings;
using UnityEngine;

namespace Element.Presentation.Overlay.Elements;

/// <summary>
/// Where one layer of a shape (its fill or its border) reads its colors from
/// <see cref="OverlayGradientTexture"/>. A solid layer reads a white texel and
/// keeps its color in the vertices.
/// </summary>
internal struct OverlayGradientLayer
{
  public bool Gradient;
  public bool Linear;
  public OverlayGradientFrame Frame;

  // Texture v of the first and last row centers (the first is higher), and
  // of the raster's top edge with how much v its rows span.
  public float RowTop,
    RowBottom,
    EdgeTop,
    Span;

  /// <summary>
  /// The texture coordinate of a box point (units, y down). A linear
  /// gradient's coordinate is affine in the point, so triangles interpolate
  /// it exactly; the other kinds map the box onto their raster.
  /// </summary>
  public Vector2 Uv(float x, float y, float width, float height)
  {
    if (!Gradient)
      return new Vector2(.5f / OverlayGradientTexture.Columns, RowTop);
    if (Linear)
      return new Vector2(
        (.5f + Frame.At(x, y) * (OverlayGradientTexture.Columns - 1)) / OverlayGradientTexture.Columns,
        RowTop
      );
    float u = width > 0 ? x / width : 0,
      v = height > 0 ? y / height : 0;
    return new Vector2(u, Mathf.Clamp(EdgeTop - v * Span, RowBottom, RowTop));
  }
}

/// <summary>
/// The colors a gradient shape samples: one texture 256 texels wide holding a
/// white row, then each gradient layer. A linear gradient is one row of its
/// colors from start to end, read at an exact coordinate per vertex. Radial,
/// angular and diamond gradients are drawn into a small raster of the box.
/// Built on the CPU only when the gradient or the shape's size changes, so it
/// works with the stock UI shader on every platform.
/// </summary>
internal sealed class OverlayGradientTexture : IDisposable
{
  public const int Columns = 256;
  private const int MaximumRasterRows = 128;

  private readonly OverlayResolvedGradient _fill = new(),
    _stroke = new();
  private Texture2D _texture;
  private Color32[] _pixels = Array.Empty<Color32>();
  private bool _hasFill,
    _hasStroke;
  private float _width,
    _height;
  private int _rasterRows;

  public Texture Texture => _texture;

  /// <summary>
  /// Rebuilds the colors for a box <paramref name="width"/> by
  /// <paramref name="height"/> units drawn <paramref name="pixelHeight"/>
  /// pixels tall; returns whether anything changed. Null layers are solid.
  /// </summary>
  public bool Update(
    OverlayResolvedGradient fill,
    OverlayResolvedGradient stroke,
    float width,
    float height,
    float pixelHeight,
    out OverlayGradientLayer fillLayer,
    out OverlayGradientLayer strokeLayer
  )
  {
    int rows = Mathf.Clamp(Mathf.CeilToInt(pixelHeight * .5f), 8, MaximumRasterRows);
    bool changed =
      _texture == null
      || (fill != null) != _hasFill
      || (stroke != null) != _hasStroke
      || (fill != null && !fill.SameAs(_fill))
      || (stroke != null && !stroke.SameAs(_stroke))
      || !Mathf.Approximately(width, _width)
      || !Mathf.Approximately(height, _height)
      || rows != _rasterRows;
    _hasFill = fill != null;
    _hasStroke = stroke != null;
    if (_hasFill)
      _fill.CopyFrom(fill);
    if (_hasStroke)
      _stroke.CopyFrom(stroke);
    _width = width;
    _height = height;
    _rasterRows = rows;

    // A white row, then each layer: a linear one in the middle of three copies
    // of its row, so filtering never reaches a neighbor; a raster as is,
    // read only between its first and last row centers.
    int fillRows = _hasFill ? Rows(_fill) : 0,
      strokeRows = _hasStroke ? Rows(_stroke) : 0;
    int total = 3 + fillRows + strokeRows;
    fillLayer = Layer(_hasFill ? _fill : null, 3, fillRows, total);
    strokeLayer = Layer(_hasStroke ? _stroke : null, 3 + fillRows, strokeRows, total);
    if (_hasFill)
      fillLayer.Frame = _fill.Frame(width, height);
    if (_hasStroke)
      strokeLayer.Frame = _stroke.Frame(width, height);
    if (!changed)
      return false;

    if (_texture == null || _texture.height != total)
    {
      SettingsUiFactory.Destroy(_texture);
      _texture = new Texture2D(Columns, total, TextureFormat.RGBA32, false)
      {
        name = "Element gradient",
        wrapMode = TextureWrapMode.Clamp,
        filterMode = FilterMode.Bilinear,
        hideFlags = HideFlags.DontSave,
      };
      _pixels = new Color32[Columns * total];
    }
    // Unity's rows run bottom up; row 0 here is the top.
    Fill(0, 3, new Color32(255, 255, 255, 255), total);
    if (_hasFill)
      Draw(_fill, 3, fillRows, total);
    if (_hasStroke)
      Draw(_stroke, 3 + fillRows, strokeRows, total);
    _texture.SetPixels32(_pixels);
    _texture.Apply(false, false);
    return true;
  }

  private int Rows(OverlayResolvedGradient gradient) =>
    gradient.Kind == OverlayGradientKind.Linear ? 3 : _rasterRows;

  private static OverlayGradientLayer Layer(OverlayResolvedGradient gradient, int top, int rows, int total)
  {
    if (gradient == null)
    {
      // The white row's center.
      float white = 1 - 1.5f / total;
      return new OverlayGradientLayer { RowTop = white, RowBottom = white };
    }
    bool linear = gradient.Kind == OverlayGradientKind.Linear;
    float first = 1 - (top + (linear ? 1.5f : .5f)) / total;
    float last = 1 - (top + (linear ? 1.5f : rows - .5f)) / total;
    return new OverlayGradientLayer
    {
      Gradient = true,
      Linear = linear,
      RowTop = first,
      RowBottom = last,
      EdgeTop = 1 - (float)top / total,
      Span = (float)rows / total,
    };
  }

  private void Draw(OverlayResolvedGradient gradient, int top, int rows, int total)
  {
    if (gradient.Kind == OverlayGradientKind.Linear)
    {
      for (int x = 0; x < Columns; x++)
      {
        Color32 color = gradient.Sample32(x / (float)(Columns - 1));
        for (int row = 0; row < 3; row++)
          _pixels[Index(top + row, x, total)] = color;
      }
      return;
    }
    OverlayGradientFrame frame = gradient.Frame(_width, _height);
    for (int row = 0; row < rows; row++)
    {
      float y = (row + .5f) / rows * _height;
      for (int x = 0; x < Columns; x++)
        _pixels[Index(top + row, x, total)] = gradient.Sample32(frame.At((x + .5f) / Columns * _width, y));
    }
  }

  private void Fill(int top, int rows, Color32 color, int total)
  {
    for (int row = top; row < top + rows; row++)
    for (int x = 0; x < Columns; x++)
      _pixels[Index(row, x, total)] = color;
  }

  private static int Index(int rowFromTop, int x, int total) => (total - 1 - rowFromTop) * Columns + x;

  public void Dispose()
  {
    SettingsUiFactory.Destroy(_texture);
    _texture = null;
  }
}
