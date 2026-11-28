using System;
using System.Collections.Generic;
using System.Numerics;

namespace Element.Domain.KeyViewer;

/// <summary>
/// The screen pixels one overlay element draws over one key viewer: runs of
/// whole pixels along rows, clipped to the viewer's frame (screen pixels, top
/// left origin). A pixel counts when its center is inside what is drawn. The
/// runs of a row may come in any order and overlap; <see cref="ViewerCoverMask"/>
/// joins them. Reused: filling again allocates nothing once the lists have grown.
/// </summary>
internal sealed class ViewerCoverRuns
{
  private static readonly int[] OneContour = { 0 };

  // Triples: row, first pixel, the pixel after the last.
  private readonly List<int> _runs = new();
  private readonly List<float> _crossings = new();
  private readonly List<Vector2> _quad = new(4);

  // The box the runs fill, so joining them looks only there.
  private int _minRow,
    _endRow,
    _minColumn,
    _endColumn;

  /// <summary>The clip, in whole screen pixels; right and bottom are outside it.</summary>
  public int Left { get; private set; }

  public int Top { get; private set; }
  public int Right { get; private set; }
  public int Bottom { get; private set; }

  /// <summary>What the runs were taken from, such as a version of the element. Reset forgets it.</summary>
  public int Stamp { get; set; } = int.MinValue;

  public int Count => _runs.Count / 3;

  /// <summary>Empties the runs and clips the next ones to the given pixels.</summary>
  public void Reset(int left, int top, int right, int bottom)
  {
    Left = left;
    Top = top;
    Right = Math.Max(left, right);
    Bottom = Math.Max(top, bottom);
    Stamp = int.MinValue;
    _runs.Clear();
    _minRow = _minColumn = int.MaxValue;
    _endRow = _endColumn = int.MinValue;
  }

  /// <summary>The box of every run in screen pixels (right and bottom outside it); false without runs.</summary>
  public bool TryGetArea(out int left, out int top, out int right, out int bottom)
  {
    left = _minColumn;
    top = _minRow;
    right = _endColumn;
    bottom = _endRow;
    return _runs.Count > 0;
  }

  public void Run(int index, out int row, out int start, out int end)
  {
    row = _runs[index * 3];
    start = _runs[index * 3 + 1];
    end = _runs[index * 3 + 2];
  }

  /// <summary>Pixels <paramref name="start"/> up to <paramref name="end"/> (not included) of <paramref name="row"/>.</summary>
  public void Add(int row, int start, int end)
  {
    if (row < Top || row >= Bottom)
      return;
    start = Math.Max(start, Left);
    end = Math.Min(end, Right);
    if (end <= start)
      return;
    _minRow = Math.Min(_minRow, row);
    _endRow = Math.Max(_endRow, row + 1);
    _minColumn = Math.Min(_minColumn, start);
    _endColumn = Math.Max(_endColumn, end);
    int last = _runs.Count - 3;
    // A run touching the one before it on the same row grows it instead.
    if (last >= 0 && _runs[last] == row && _runs[last + 1] <= end && _runs[last + 2] >= start)
    {
      _runs[last + 1] = Math.Min(_runs[last + 1], start);
      _runs[last + 2] = Math.Max(_runs[last + 2], end);
      return;
    }
    _runs.Add(row);
    _runs.Add(start);
    _runs.Add(end);
  }

  /// <summary>An axis-aligned rectangle in screen pixels.</summary>
  public void FillRect(float x, float y, float width, float height)
  {
    if (!(width > 0 && height > 0) || !Finite(x) || !Finite(y) || !Finite(width) || !Finite(height))
      return;
    int start = FirstPixel(x, Left, Right),
      end = FirstPixel(x + width, Left, Right);
    int first = Math.Max(Top, FirstPixel(y, Top, Bottom)),
      after = Math.Min(Bottom, FirstPixel(y + height, Top, Bottom));
    for (int row = first; row < after; row++)
      Add(row, start, end);
  }

  /// <summary>A quadrilateral given by its corners in order, such as a glyph's box.</summary>
  public void FillQuad(Vector2 a, Vector2 b, Vector2 c, Vector2 d)
  {
    _quad.Clear();
    _quad.Add(a);
    _quad.Add(b);
    _quad.Add(c);
    _quad.Add(d);
    FillEvenOdd(_quad, OneContour);
  }

  /// <summary>
  /// The closed outlines made of <paramref name="points"/> (screen pixels),
  /// filled with the even-odd rule: an outline inside another cuts a hole, so
  /// an outer and an inner outline make a ring. <paramref name="contourStarts"/>
  /// holds the index each outline starts at; the last runs to the end.
  /// </summary>
  public void FillEvenOdd(IReadOnlyList<Vector2> points, IReadOnlyList<int> contourStarts)
  {
    if (points == null || contourStarts == null || points.Count < 3 || contourStarts.Count == 0)
      return;
    float top = float.PositiveInfinity,
      bottom = float.NegativeInfinity;
    for (int i = 0; i < points.Count; i++)
    {
      Vector2 point = points[i];
      if (!Finite(point.X) || !Finite(point.Y))
        return;
      top = Math.Min(top, point.Y);
      bottom = Math.Max(bottom, point.Y);
    }
    int first = Math.Max(Top, FirstPixel(top, Top, Bottom)),
      after = Math.Min(Bottom, FirstPixel(bottom, Top, Bottom));
    for (int row = first; row < after; row++)
    {
      // Where the outlines cross the middle of this row of pixels.
      float y = row + .5f;
      _crossings.Clear();
      for (int contour = 0; contour < contourStarts.Count; contour++)
      {
        int from = contourStarts[contour],
          to = contour + 1 < contourStarts.Count ? contourStarts[contour + 1] : points.Count;
        if (from < 0 || to > points.Count || to - from < 3)
          continue;
        for (int i = from, j = to - 1; i < to; j = i++)
        {
          Vector2 a = points[j],
            b = points[i];
          // Half open, so a corner on the row's middle counts once.
          if ((a.Y <= y) == (b.Y <= y))
            continue;
          _crossings.Add(a.X + (y - a.Y) * (b.X - a.X) / (b.Y - a.Y));
        }
      }
      SortCrossings();
      for (int k = 0; k + 1 < _crossings.Count; k += 2)
        Add(row, FirstPixel(_crossings[k], Left, Right), FirstPixel(_crossings[k + 1], Left, Right));
    }
  }

  // Few crossings per row (two for a convex outline): insertion sort, no allocation.
  private void SortCrossings()
  {
    for (int i = 1; i < _crossings.Count; i++)
    {
      float value = _crossings[i];
      int j = i - 1;
      while (j >= 0 && _crossings[j] > value)
      {
        _crossings[j + 1] = _crossings[j];
        j--;
      }
      _crossings[j + 1] = value;
    }
  }

  /// <summary>
  /// The first pixel whose center is at or after <paramref name="edge"/>,
  /// kept one pixel outside <paramref name="low"/>..<paramref name="high"/> so
  /// far-off edges stay whole numbers.
  /// </summary>
  public static int FirstPixel(float edge, int low, int high)
  {
    float value = Math.Max(low - 1, Math.Min(high + 1, edge - .5f));
    return (int)Math.Ceiling(value);
  }

  private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}
