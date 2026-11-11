using System;
using Element.Configuration.Overlay;
using Element.Domain.Overlay.Gradient;
using Element.Presentation.Ui.Widgets;
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
