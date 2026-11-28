using System;
using Element.Domain.KeyViewer;
using Element.Presentation.Overlay.Fonts;
using UnityEngine;
using NVector2 = System.Numerics.Vector2;

namespace Element.Presentation.Overlay.Text;

/// <summary>
/// Traces the pixels one glyph draws from its signed distance field, on the
/// CPU: for each screen pixel inside the glyph's quad, the atlas texel under
/// its center decides, by the material's thresholds (<see cref="GlyphShading"/>),
/// whether the face, its outline or its shadow is drawn there. Only pixels
/// inside the clip of the runs are looked at, so a text that overlaps a key
/// viewer a little costs a little.
/// </summary>
internal static class GlyphCover
{
  /// <summary>
  /// The glyph's quad by three corners in screen pixels: <paramref name="bottomLeft"/>
  /// at <paramref name="uvMin"/>, the bottom right and top left one step along
  /// each UV axis; <paramref name="uvMax"/> is the top right's UV.
  /// </summary>
  public static void Trace(
    ViewerCoverRuns runs,
    NVector2 bottomLeft,
    NVector2 bottomRight,
    NVector2 topLeft,
    Vector2 uvMin,
    Vector2 uvMax,
    in FontAtlasAlpha atlas,
    in GlyphShading shading,
    bool bold
  )
  {
    NVector2 across = bottomRight - bottomLeft,
      up = topLeft - bottomLeft;
    float determinant = across.X * up.Y - across.Y * up.X;
    if (Math.Abs(determinant) < 1e-6f)
      return;
    // How far along each edge (0 to 1) a step of one pixel goes: italic quads lean.
    float sx = up.Y / determinant,
      sy = -up.X / determinant;
    float tx = -across.Y / determinant,
      ty = across.X / determinant;
    NVector2 topRight = bottomRight + up;
    float left = Math.Min(Math.Min(bottomLeft.X, bottomRight.X), Math.Min(topLeft.X, topRight.X)),
      right = Math.Max(Math.Max(bottomLeft.X, bottomRight.X), Math.Max(topLeft.X, topRight.X)),
      top = Math.Min(Math.Min(bottomLeft.Y, bottomRight.Y), Math.Min(topLeft.Y, topRight.Y)),
      bottom = Math.Max(Math.Max(bottomLeft.Y, bottomRight.Y), Math.Max(topLeft.Y, topRight.Y));
    int firstColumn = Math.Max(runs.Left, ViewerCoverRuns.FirstPixel(left, runs.Left, runs.Right)),
      endColumn = Math.Min(runs.Right, ViewerCoverRuns.FirstPixel(right, runs.Left, runs.Right));
    int firstRow = Math.Max(runs.Top, ViewerCoverRuns.FirstPixel(top, runs.Top, runs.Bottom)),
      endRow = Math.Min(runs.Bottom, ViewerCoverRuns.FirstPixel(bottom, runs.Top, runs.Bottom));
    if (firstColumn >= endColumn || firstRow >= endRow)
      return;
    float du = uvMax.x - uvMin.x,
      dv = uvMax.y - uvMin.y;
    int face = bold ? shading.BoldFace : shading.Face;
    int shadowFace = bold ? shading.BoldShadowFace : shading.ShadowFace;
    bool shadow = shading.Shadow && shadowFace <= 255;
    // A glyph's bottom edge runs along the text, so v stays the same along a
    // row of pixels (italic leans only the sides): each row reads one atlas row.
    bool level = Math.Abs(tx) < 1e-7f;
    for (int row = firstRow; row < endRow; row++)
    {
      float x = firstColumn + .5f - bottomLeft.X,
        y = row + .5f - bottomLeft.Y;
      float s = sx * x + sy * y,
        t = tx * x + ty * y;
      if (level && !(t >= 0 && t <= 1))
        continue;
      float u = uvMin.x + s * du,
        v = uvMin.y + t * dv;
      float stepU = sx * du,
        stepV = tx * dv;
      int faceRow = atlas.Row(v),
        shadowRow = shadow ? atlas.Row(v + shading.ShadowV) : -1;
      int start = -1;
      for (int column = firstColumn; column < endColumn; column++, s += sx, t += tx, u += stepU, v += stepV)
      {
        bool drawn = false;
        if (s >= 0 && s <= 1 && t >= 0 && t <= 1)
        {
          if (!level)
          {
            faceRow = atlas.Row(v);
            shadowRow = shadow ? atlas.Row(v + shading.ShadowV) : -1;
          }
          drawn =
            atlas.At(faceRow, u) >= face || (shadowRow >= 0 && atlas.At(shadowRow, u + shading.ShadowU) >= shadowFace);
        }
        if (drawn)
        {
          if (start < 0)
            start = column;
        }
        else if (start >= 0)
        {
          runs.Add(row, start, column);
          start = -1;
        }
      }
      if (start >= 0)
        runs.Add(row, start, endColumn);
    }
  }
}
