using System;
using System.Collections.Generic;
using Element.Domain.Layout;

namespace Element.Domain.KeyViewer;

/// <summary>
/// Everything that covers one key viewer, joined into rectangles that never
/// overlap, so the native hosts can cut them out with an even-odd fill. One
/// bit per screen pixel over the viewer's frame; each row's runs become
/// rectangles that grow downward while the next row has the same run.
/// <para>
/// More than <see cref="MaximumSpans"/> rectangles (long text traced pixel by
/// pixel) make the grid coarser, 2, 4, 8… pixels a cell, until they fit. A
/// coarse cell is covered when any of its pixels is, so the cut never shows
/// less of an element than it draws; it only shows a little of the game
/// around it. Reused: allocates nothing once its buffers have grown.
/// </para>
/// </summary>
internal sealed class ViewerCoverMask
{
  /// <summary>
  /// The most rectangles one viewer gets. Windows sends them to the page as
  /// script text (about 30 characters each) every time they change, so this
  /// keeps a combo hit well under a millisecond on the game's thread.
  /// </summary>
  public const int MaximumSpans = 512;

  private ulong[] _bits = Array.Empty<ulong>();
  private int _left,
    _top,
    _width,
    _height;
  private int _columns,
    _rows,
    _stride,
    _cell = 1;

  // Rectangles still growing downward, and those the current row keeps
  // growing: triples of first column, column after the last, first row.
  private List<int> _open = new();
  private List<int> _carry = new();
  private readonly List<int> _row = new();

  /// <summary>Screen pixels per cell of the grid: 1 until the rectangles needed a coarser one.</summary>
  public int Cell => _cell;

  /// <summary>Empties the mask over the frame <paramref name="left"/>, <paramref name="top"/> (screen pixels).</summary>
  public void Reset(int left, int top, int width, int height)
  {
    _left = left;
    _top = top;
    _width = Math.Max(0, width);
    _height = Math.Max(0, height);
    _cell = 1;
    _columns = _width;
    _rows = _height;
    _stride = (_columns + 63) >> 6;
    int words = _stride * _rows;
    if (_bits.Length < words)
      _bits = new ulong[words];
    else
      Array.Clear(_bits, 0, words);
  }

  /// <summary>Adds the pixels of <paramref name="runs"/>; those outside the frame are left out.</summary>
  public void Add(ViewerCoverRuns runs)
  {
    if (runs == null || _cell != 1)
      return;
    for (int i = 0; i < runs.Count; i++)
    {
      runs.Run(i, out int row, out int start, out int end);
      row -= _top;
      start = Math.Max(0, start - _left);
      end = Math.Min(_columns, end - _left);
      if (row >= 0 && row < _rows && end > start)
        Set(row, start, end);
    }
  }

  /// <summary>
  /// The covered pixels as rectangles in screen pixels, at most
  /// <paramref name="maximum"/> of them; empty when nothing covers the viewer.
  /// </summary>
  public void Spans(List<LayoutBounds> output, int maximum = MaximumSpans)
  {
    output.Clear();
    maximum = Math.Max(1, maximum);
    while (!TrySpans(output, maximum))
    {
      output.Clear();
      Coarsen();
    }
  }

  private void Set(int row, int start, int end)
  {
    int line = row * _stride;
    int first = start >> 6,
      last = (end - 1) >> 6;
    ulong head = ~0UL << (start & 63);
    ulong tail = ~0UL >> (63 - ((end - 1) & 63));
    if (first == last)
    {
      _bits[line + first] |= head & tail;
      return;
    }
    _bits[line + first] |= head;
    for (int word = first + 1; word < last; word++)
      _bits[line + word] = ~0UL;
    _bits[line + last] |= tail;
  }

  // False when the rectangles would be more than the maximum.
  private bool TrySpans(List<LayoutBounds> output, int maximum)
  {
    _open.Clear();
    for (int row = 0; row <= _rows; row++)
    {
      _row.Clear();
      if (row < _rows)
        ReadRow(row);
      _carry.Clear();
      int open = 0;
      for (int k = 0; k < _row.Count; k += 2)
      {
        int start = _row[k],
          end = _row[k + 1];
        // Rectangles that start further left can't continue in this row any more.
        while (open < _open.Count && _open[open] < start)
        {
          Emit(output, open, row);
          open += 3;
        }
        if (open < _open.Count && _open[open] == start && _open[open + 1] == end)
        {
          _carry.Add(start);
          _carry.Add(end);
          _carry.Add(_open[open + 2]);
          open += 3;
        }
        else
        {
          _carry.Add(start);
          _carry.Add(end);
          _carry.Add(row);
        }
      }
      for (; open < _open.Count; open += 3)
        Emit(output, open, row);
      if (output.Count > maximum)
        return false;
      (_open, _carry) = (_carry, _open);
    }
    return true;
  }

  // Runs of set bits in one row of the grid, as pairs of first and after-last
  // column. Jumps from one change of bit to the next, so a word costs as much
  // as the runs that start or end in it.
  private void ReadRow(int row)
  {
    int line = row * _stride;
    int start = -1;
    for (int word = 0; word < _stride; word++)
    {
      ulong bits = _bits[line + word];
      int bit = 0;
      while (bit < 64)
      {
        // Inside a run, look for its first clear bit; outside, for the next set one.
        ulong rest = (start >= 0 ? ~bits : bits) >> bit;
        if (rest == 0)
          break;
        bit += LowestBit(rest);
        int column = (word << 6) + bit;
        if (start < 0)
          start = column;
        else
        {
          _row.Add(start);
          _row.Add(column);
          start = -1;
        }
      }
    }
    if (start >= 0)
    {
      _row.Add(start);
      _row.Add(_columns);
    }
  }

  // The index of the lowest set bit of a value that isn't 0, by halving.
  private static int LowestBit(ulong value)
  {
    int bit = 0;
    if ((value & 0xFFFFFFFFUL) == 0)
    {
      bit += 32;
      value >>= 32;
    }
    if ((value & 0xFFFFUL) == 0)
    {
      bit += 16;
      value >>= 16;
    }
    if ((value & 0xFFUL) == 0)
    {
      bit += 8;
      value >>= 8;
    }
    if ((value & 0xFUL) == 0)
    {
      bit += 4;
      value >>= 4;
    }
    if ((value & 0x3UL) == 0)
    {
      bit += 2;
      value >>= 2;
    }
    return (value & 0x1UL) == 0 ? bit + 1 : bit;
  }

  private void Emit(List<LayoutBounds> output, int open, int row)
  {
    int x = _open[open] * _cell,
      y = _open[open + 2] * _cell;
    int width = Math.Min(_open[open + 1] * _cell, _width) - x,
      height = Math.Min(row * _cell, _height) - y;
    if (width > 0 && height > 0)
      output.Add(new LayoutBounds(_left + x, _top + y, width, height));
  }

  // Halves the grid in place: a cell is covered when any of the four it replaces was.
  private void Coarsen()
  {
    int columns = (_columns + 1) >> 1,
      rows = (_rows + 1) >> 1,
      stride = (columns + 63) >> 6;
    // Every new word is written after the old words it reads, at an index no
    // later than theirs, and later new words read only later old ones.
    for (int row = 0; row < rows; row++)
    for (int word = 0; word < stride; word++)
    {
      ulong low = Old(row * 2, word * 2) | Old(row * 2 + 1, word * 2);
      ulong high = Old(row * 2, word * 2 + 1) | Old(row * 2 + 1, word * 2 + 1);
      _bits[row * stride + word] = Pairs(low) | (Pairs(high) << 32);
    }
    _columns = columns;
    _rows = rows;
    _stride = stride;
    _cell *= 2;
  }

  private ulong Old(int row, int word) => row < _rows && word < _stride ? _bits[row * _stride + word] : 0;

  // Bit i of the result is set when bit 2i or 2i + 1 of value is.
  private static ulong Pairs(ulong value)
  {
    value = (value | (value >> 1)) & 0x5555555555555555UL;
    value = (value | (value >> 1)) & 0x3333333333333333UL;
    value = (value | (value >> 2)) & 0x0F0F0F0F0F0F0F0FUL;
    value = (value | (value >> 4)) & 0x00FF00FF00FF00FFUL;
    value = (value | (value >> 8)) & 0x0000FFFF0000FFFFUL;
    value = (value | (value >> 16)) & 0x00000000FFFFFFFFUL;
    return value;
  }
}
