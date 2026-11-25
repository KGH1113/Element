using System.Collections.Generic;
using Element.Domain.Overlay.Text;
using Element.Domain.Overlay.Variables;
using Element.Presentation.OverlayEditor.Parts.Field;
using Element.Presentation.Ui.Theme;
using Element.Presentation.Ui.Widgets;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Element.Presentation.OverlayEditor.TextEditor;

/// <summary>
/// The text is its own content field: under each piece of it (each 글자
/// value, each value of the game) a thin bracket in its type's soft color,
/// and under that its type's mark. Nothing is drawn over the letters, so the
/// preview stays what the game draws. The picked piece's bracket is the
/// accent and thicker; the one under the pointer is brighter. A piece across
/// lines gets a bracket on each line and its mark on the last; a mark that
/// would reach the next line shows only for the picked or pointed piece.
/// </summary>
internal sealed class TextUnderlines
{
  // In frame units, kept the same at any text size.
  private const float Gap = 3,
    Depth = 5,
    Thin = 1.5f,
    Thick = 2.5f,
    Inset = 1.5f,
    MarkGap = 4,
    MarkSize = 16,
    GlyphSize = 11;

  private static readonly Color Bright = new(1, 1, 1, .85f);

  private readonly TextDocumentEditing _text;
  private readonly TextPreview _preview;
  private readonly TextLayoutMap _map;
  private readonly List<Piece> _pieces = new();
  private readonly List<Segment> _segments = new();
  private readonly List<Image> _bars = new();
  private readonly List<Image> _tiles = new();
  private readonly List<TextMeshProUGUI> _glyphs = new();
  private UiFactory _ui;
  private int _hovered = -1;
  private int _piecesUsed,
    _barsUsed,
    _tilesUsed;

  public TextUnderlines(TextDocumentEditing text, TextPreview preview, TextLayoutMap map)
  {
    _text = text;
    _preview = preview;
    _map = map;
  }

  public void Build(UiFactory ui)
  {
    _ui = ui;
    _bars.Clear();
    _tiles.Clear();
    _glyphs.Clear();
    _piecesUsed = 0;
    _hovered = -1;
  }

  /// <summary>
  /// Brackets and marks for the text as laid out now. <paramref name="picked"/>
  /// is the value open in the dock (-1 for none); with none, the 글자 value
  /// the caret types into is lit. While <paramref name="lookOpen"/> (a look
  /// field of the column is open), selected letters show as their own 글자
  /// value, as they become once the look is changed, and the caret's letters
  /// rest.
  /// </summary>
  public void Place(int picked, bool lookOpen)
  {
    if (_ui == null)
      return;
    Collect(picked, lookOpen);
    _barsUsed = _tilesUsed = 0;
    float unit = 1 / Mathf.Max(.01f, _preview.Scale);
    int lastLine = _map.Lines - 1;
    for (int p = 0; p < _piecesUsed; p++)
      Draw(_pieces[p], unit, lastLine);
    Hide(_bars, _barsUsed);
    Hide(_tiles, _tilesUsed);
  }

  /// <summary>The item under the pointer, its piece brighter; -1 for none.</summary>
  public void Hover(int item)
  {
    if (item == _hovered)
      return;
    _hovered = item;
    // Only colors change: the pieces and their places stay.
    for (int p = 0; p < _piecesUsed; p++)
    {
      Piece piece = _pieces[p];
      Color ink = Ink(piece);
      for (int i = piece.FirstBar; i < piece.FirstBar + piece.Bars; i++)
        _bars[i].color = ink;
      if (piece.Tile >= 0)
        _tiles[piece.Tile].gameObject.SetActive(MarkShown(piece));
    }
  }

  /// <summary>How many pieces are marked, for the checks.</summary>
  public int Pieces => _piecesUsed;

  // The pieces in order: values one by one, letters run by run, the
  // selection on its own while it is split off.
  private void Collect(int picked, bool lookOpen)
  {
    _piecesUsed = 0;
    OverlayTextDocument document = _text.Document;
    _text.Range(out int selStart, out int selEnd);
    bool splitting = lookOpen && selStart < selEnd;
    bool typing = picked < 0 && !lookOpen;
    bool caretRun = OverlayTextRuns.RunOfCaret(document, _text.Caret, out int caretStart, out int caretEnd);
    int i = 0;
    while (i < document.Count)
    {
      if (document[i].IsValue)
      {
        Add(i, i + 1, OverlayVariableTypes.TypeOf(OverlayVariable.Parse(document[i].Value)), i == picked);
        i++;
        continue;
      }
      OverlayTextRuns.RunAt(document, i, out _, out int end);
      if (splitting && i < selEnd && selStart < end)
      {
        // Before the selection, the selection, after it.
        if (i < selStart)
          Add(i, selStart, OverlayValueType.Text, false);
        Add(Mathf.Max(i, selStart), Mathf.Min(end, selEnd), OverlayValueType.Text, true);
        if (selEnd < end)
          Add(selEnd, end, OverlayValueType.Text, false);
      }
      else
        Add(i, end, OverlayValueType.Text, typing && caretRun && caretStart == i && caretEnd == end);
      i = end;
    }
  }

  private void Add(int start, int end, OverlayValueType type, bool lit)
  {
    if (_piecesUsed >= _pieces.Count)
      _pieces.Add(new Piece());
    Piece piece = _pieces[_piecesUsed++];
    piece.Start = start;
    piece.End = end;
    piece.Type = type;
    piece.Lit = lit;
  }

  private void Draw(Piece piece, float unit, int lastLine)
  {
    piece.FirstBar = _barsUsed;
    piece.Bars = 0;
    piece.Tile = -1;
    Segments(piece);
    if (_segments.Count == 0)
      return;
    float thickness = (piece.Lit ? Thick : Thin) * unit;
    foreach (Segment segment in _segments)
      Bracket(piece, segment, unit, thickness);
    Segment last = _segments[_segments.Count - 1];
    piece.MarkAlways = last.Line >= lastLine;
    piece.Tile = Mark(piece, last, unit);
    _tiles[piece.Tile].gameObject.SetActive(MarkShown(piece));
  }

  // Where the piece's letters are, a span per line.
  private void Segments(Piece piece)
  {
    _segments.Clear();
    for (int item = piece.Start; item < piece.End; item++)
    {
      if (_map.IsLineBreak(item) || !_map.ItemCharacters(item, out TMP_CharacterInfo first, out TMP_CharacterInfo last))
        continue;
      float left = first.origin,
        right = Mathf.Max(last.xAdvance, first.origin + 2);
      int count = _segments.Count;
      if (count > 0 && _segments[count - 1].Line == first.lineNumber)
      {
        Segment segment = _segments[count - 1];
        segment.Left = Mathf.Min(segment.Left, left);
        segment.Right = Mathf.Max(segment.Right, right);
        _segments[count - 1] = segment;
      }
      else
        _segments.Add(new Segment(first.lineNumber, left, right));
    }
  }

  // A bracket under one line of a piece: a bottom stroke and a short tick at each end.
  private void Bracket(Piece piece, Segment segment, float unit, float thickness)
  {
    _map.LineExtent(segment.Line, out _, out float bottom);
    float top = bottom - Gap * unit,
      low = top - Depth * unit;
    float left = segment.Left + Inset * unit,
      right = Mathf.Max(left + thickness * 2, segment.Right - Inset * unit);
    Color ink = Ink(piece);
    Bar(left, low, right - left, thickness, ink);
    Bar(left, low, thickness, top - low, ink);
    Bar(right - thickness, low, thickness, top - low, ink);
    piece.Bars += 3;
  }

  private void Bar(float x, float y, float width, float height, Color color)
  {
    if (_barsUsed >= _bars.Count)
    {
      Image image = _ui.Panel("Underline", _preview.Text.rectTransform, 0, 0, 1, 1, color, false);
      Anchor(image.rectTransform);
      _bars.Add(image);
    }
    Image bar = _bars[_barsUsed++];
    if (!bar.gameObject.activeSelf)
      bar.gameObject.SetActive(true);
    bar.color = color;
    bar.rectTransform.anchoredPosition = new Vector2(x, y);
    bar.rectTransform.sizeDelta = new Vector2(width, height);
  }

  // The type's mark, centered under the piece's last line.
  private int Mark(Piece piece, Segment segment, float unit)
  {
    _map.LineExtent(segment.Line, out _, out float bottom);
    float size = MarkSize * unit;
    float top = bottom - (Gap + Depth + MarkGap) * unit;
    if (_tilesUsed >= _tiles.Count)
    {
      Image made = _ui.Panel("Type mark", _preview.Text.rectTransform, 0, 0, 1, 1, Color.white);
      Anchor(made.rectTransform);
      TextMeshProUGUI glyph = _ui.Text("", made.rectTransform, 0, 0, 1, 1, GlyphSize);
      glyph.alignment = TextAlignmentOptions.Center;
      glyph.textWrappingMode = TextWrappingModes.NoWrap;
      glyph.overflowMode = TextOverflowModes.Overflow;
      UiFactory.Stretch(glyph.rectTransform, 0, 0, 0, 0);
      _tiles.Add(made);
      _glyphs.Add(glyph);
    }
    int index = _tilesUsed++;
    Image tile = _tiles[index];
    Color ink = TypeMarks.InkColor(piece.Type);
    tile.color = new Color(ink.r, ink.g, ink.b, .16f);
    // The rounding stays the same on screen at any text size.
    tile.pixelsPerUnitMultiplier = 2f * Mathf.Max(.01f, _preview.Scale);
    tile.rectTransform.anchoredPosition = new Vector2((segment.Left + segment.Right - size) * .5f, top - size);
    tile.rectTransform.sizeDelta = new Vector2(size, size);
    TextMeshProUGUI text = _glyphs[index];
    text.text = TypeMarks.Glyph(piece.Type);
    text.color = ink;
    text.fontSize = GlyphSize * unit;
    return index;
  }

  private bool MarkShown(Piece piece) => piece.MarkAlways || piece.Lit || Hovered(piece);

  private bool Hovered(Piece piece) => _hovered >= piece.Start && _hovered < piece.End;

  private Color Ink(Piece piece)
  {
    if (piece.Lit)
      return UiPalette.SoftAccent;
    if (Hovered(piece))
      return Bright;
    Color ink = TypeMarks.InkColor(piece.Type);
    ink.a = piece.Type == OverlayValueType.Any ? .5f : .55f;
    return ink;
  }

  private static void Hide<T>(List<T> pool, int used)
    where T : Component
  {
    for (int i = used; i < pool.Count; i++)
      if (pool[i].gameObject.activeSelf)
        pool[i].gameObject.SetActive(false);
  }

  private static void Anchor(RectTransform rect)
  {
    rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
    rect.pivot = Vector2.zero;
  }

  private sealed class Piece
  {
    public int Start;
    public int End;
    public OverlayValueType Type;
    public bool Lit;
    public int FirstBar;
    public int Bars;
    public int Tile = -1;
    public bool MarkAlways = true;
  }

  private struct Segment
  {
    public Segment(int line, float left, float right)
    {
      Line = line;
      Left = left;
      Right = right;
    }

    public readonly int Line;
    public float Left;
    public float Right;
  }
}
