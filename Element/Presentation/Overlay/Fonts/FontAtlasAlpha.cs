using System;
using Unity.Collections;

namespace Element.Presentation.Overlay.Fonts;

/// <summary>
/// A font atlas's alpha on the CPU, read from the texel a UV falls in. A view
/// into memory <see cref="FontAtlasPixels"/> owns: use it during one drawing only.
/// </summary>
internal readonly struct FontAtlasAlpha
{
  private readonly NativeArray<byte> _data;
  private readonly int _step,
    _offset;

  public readonly int Width,
    Height;

  /// <param name="step">Bytes per texel.</param>
  /// <param name="offset">Where alpha sits in a texel.</param>
  public FontAtlasAlpha(NativeArray<byte> data, int width, int height, int step, int offset)
  {
    _data = data;
    Width = width;
    Height = height;
    _step = step;
    _offset = offset;
  }

  /// <summary>Alpha 0–255 at <paramref name="u"/>, <paramref name="v"/> (0–1, v up as in the texture); 0 outside.</summary>
  public int At(float u, float v) => At(Row(v), u);

  /// <summary>The texel row <paramref name="v"/> falls in, for <see cref="At(int, float)"/>; -1 outside.</summary>
  public int Row(float v)
  {
    if (!(v >= 0 && v < 1))
      return -1;
    // Rounding can land a UV just under 1 on the texel after the last.
    return Math.Min(Height - 1, (int)(v * Height)) * Width;
  }

  /// <summary>Alpha 0–255 at <paramref name="u"/> along a texel row from <see cref="Row"/>; 0 outside.</summary>
  public int At(int row, float u)
  {
    if (row < 0 || !(u >= 0 && u < 1))
      return 0;
    int x = Math.Min(Width - 1, (int)(u * Width));
    return _data[(row + x) * _step + _offset];
  }
}
