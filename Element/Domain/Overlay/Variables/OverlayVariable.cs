using System;
using System.Collections.Generic;
using System.Globalization;
using Element.Domain.Overlay.Values;

namespace Element.Domain.Overlay.Variables;

/// <summary>
/// What a field holds: values side by side, numbers joined by + − × ÷ (× and
/// ÷ first). A condition is a value with five slots, each a variable of its
/// own, and a group is a variable inside the variable. What a field's type
/// allows is <see cref="OverlayVariableTypes"/>'s to say;
/// <see cref="OverlayVariableSyntax"/> reads and saves it and
/// <see cref="OverlayVariableEvaluator"/> works it out.
/// </summary>
internal sealed class OverlayVariable
{
  /// <summary>Most pieces one variable holds, slots counted apart.</summary>
  public const int MaxPieces = 63;

  /// <summary>Most digits a typed number has.</summary>
  public const int MaxDigits = 9;

  private readonly List<OverlayPiece> _pieces = new();

  public OverlayVariable() { }

  public OverlayVariable(params OverlayPiece[] pieces)
  {
    foreach (OverlayPiece piece in pieces)
      if (piece != null)
        _pieces.Add(piece);
  }

  public int Count => _pieces.Count;

  public OverlayPiece this[int index] => _pieces[index];

  public bool IsEmpty => _pieces.Count == 0;

  /// <summary>The last piece, or null when it is empty.</summary>
  public OverlayPiece Last => _pieces.Count > 0 ? _pieces[_pieces.Count - 1] : null;

  public static OverlayVariable Parse(string text) => OverlayVariableSyntax.Parse(text);

  public string Serialize() => OverlayVariableSyntax.Serialize(this);

  public static OverlayVariable FromNumber(double number) => new(OverlayPiece.NumberPiece(number));

  public static OverlayVariable FromValue(OverlayValueId id) => new(OverlayPiece.ValuePiece(id));

  public OverlayVariable Clone()
  {
    OverlayVariable copy = new();
    foreach (OverlayPiece piece in _pieces)
      copy._pieces.Add(piece.Clone());
    return copy;
  }

  public bool SameAs(OverlayVariable other)
  {
    if (other == null || other._pieces.Count != _pieces.Count)
      return false;
    for (int i = 0; i < _pieces.Count; i++)
      if (!_pieces[i].SameAs(other._pieces[i]))
        return false;
    return true;
  }

  public void Add(OverlayPiece piece) => _pieces.Add(piece);

  public void Insert(int index, OverlayPiece piece) =>
    _pieces.Insert(Math.Max(0, Math.Min(_pieces.Count, index)), piece);

  public void Set(int index, OverlayPiece piece) => _pieces[index] = piece;

  public void RemoveAt(int index) => _pieces.RemoveAt(index);

  public void Clear() => _pieces.Clear();

  /// <summary>Takes <paramref name="other"/>'s pieces in place of its own.</summary>
  public void CopyFrom(OverlayVariable other)
  {
    _pieces.Clear();
    if (other == null)
      return;
    foreach (OverlayPiece piece in other._pieces)
      _pieces.Add(piece.Clone());
  }

  public int IndexOf(OverlayPiece piece) => _pieces.IndexOf(piece);

  /// <summary>One live value and nothing else.</summary>
  public bool IsSingleValue(out OverlayValueId id)
  {
    id = default;
    if (_pieces.Count != 1 || _pieces[0].Kind != OverlayPieceKind.Value)
      return false;
    id = _pieces[0].Value;
    return true;
  }

  /// <summary>One plain number: a field holding it keeps the number, not a formula.</summary>
  public bool IsPlainNumber(out double number)
  {
    number = 0;
    if (_pieces.Count != 1 || _pieces[0].Kind != OverlayPieceKind.Number || !_pieces[0].HasNumber)
      return false;
    number = _pieces[0].NumberValue;
    return true;
  }

  /// <summary>Words that stand alone: the song title, a label.</summary>
  public bool IsWords =>
    IsSingleValue(out OverlayValueId id) && OverlayValueCatalog.Get(id).Kind == OverlayValueKind.Text;

  /// <summary>It reads a live value somewhere, its slots included.</summary>
  public bool HasValues => !Mask.IsEmpty;

  /// <summary>The live values it reads, in its slots too.</summary>
  public OverlayValueMask Mask => OverlayVariableTypes.Mask(this, 0);

  /// <summary>The number it gives, false when a value has no data, it isn't a number, or it isn't finite.</summary>
  public bool TryNumber(IOverlayNumberSource source, out double result, out bool isTime) =>
    OverlayVariableEvaluator.TryNumber(this, source, out result, out isTime);

  /// <summary>The color it gives: a piece that is a color picked by hand, or a color value; false when it gives none.</summary>
  public bool TryColor(IOverlayNumberSource source, out OverlayPiece color) =>
    OverlayVariableEvaluator.TryColor(this, source, out color);

  public bool TryBool(IOverlayNumberSource source, out bool value) =>
    OverlayVariableEvaluator.TryBool(this, source, out value);

  /// <summary>The point it gives now: one picked, or what its condition gives.</summary>
  public bool TryPoint(IOverlayNumberSource source, out Element.Configuration.Overlay.OverlayGrowth point) =>
    OverlayVariableEvaluator.TryPoint(this, source, out point);

  /// <summary>A number as saved: up to two decimals, "0" for one that isn't finite.</summary>
  public static string NumberText(double number)
  {
    if (double.IsNaN(number) || double.IsInfinity(number))
      return "0";
    return Math.Round(number, 2).ToString("0.##", CultureInfo.InvariantCulture);
  }

  /// <summary>+ - * / and their display forms (− × ÷ x) become + - * /; '\0' for none.</summary>
  public static char NormalizeOperator(char c) =>
    c switch
    {
      '+' => '+',
      '-' or '−' => '-',
      '*' or '×' or 'x' or 'X' => '*',
      '/' or '÷' => '/',
      _ => '\0',
    };

  /// <summary>How an operator reads: − × ÷ for - * /.</summary>
  public static char Symbol(char op) =>
    op switch
    {
      '-' => '−',
      '*' => '×',
      '/' => '÷',
      _ => op,
    };

  /// <summary>
  /// Where the run of numbers joined by operators that starts at
  /// <paramref name="start"/> ends (exclusive): one piece alone, or a
  /// calculation. Pieces with nothing between them sit side by side.
  /// </summary>
  public int SegmentEnd(int start)
  {
    int end = start + 1;
    while (
      end + 1 < _pieces.Count
      && _pieces[end].Kind == OverlayPieceKind.Operator
      && _pieces[end + 1].Kind != OverlayPieceKind.Operator
    )
      end += 2;
    return Math.Min(end, _pieces.Count);
  }

  /// <summary>Readable form for tooltips: <c>Combo × 2</c>, a condition as "if …".</summary>
  public string Describe(Func<OverlayValueDefinition, string> name, OverlayVariableWords words) =>
    OverlayVariableSyntax.Describe(this, name, words);
}
