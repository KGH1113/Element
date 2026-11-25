using System;
using Element.Domain.Overlay.Values;

namespace Element.Domain.Overlay.Variables;

/// <summary>
/// Works a variable out. Runs each time a value a text shows changes, so
/// nothing here allocates. A condition gives its result while its
/// comparison holds and its default otherwise, also while a value it
/// compares has no data yet.
/// </summary>
internal static class OverlayVariableEvaluator
{
  // Made values may use made values; this many deep is enough, and stops a loop.
  private const int MaxDepth = 16;

  /// <summary>
  /// The number a variable gives, × ÷ before + −. <paramref name="isTime"/>
  /// is true when the result is a time (a time not divided by another time).
  /// False when a value has no data, a piece isn't a number, pieces sit side
  /// by side, or the result isn't finite.
  /// </summary>
  public static bool TryNumber(
    OverlayVariable variable,
    IOverlayNumberSource source,
    out double result,
    out bool isTime
  ) => TryNumber(variable, source, 0, out result, out isTime);

  /// <summary>The color a variable gives: a color picked by hand, or a color value such as the highlight color.</summary>
  public static bool TryColor(OverlayVariable variable, IOverlayNumberSource source, out OverlayPiece color) =>
    TrySingle(variable, source, 0, OverlayValueType.Color, out color);

  /// <summary>True or false, as the variable gives it.</summary>
  public static bool TryBool(OverlayVariable variable, IOverlayNumberSource source, out bool value)
  {
    value = false;
    if (!TrySingle(variable, source, 0, OverlayValueType.Bool, out OverlayPiece piece))
      return false;
    value = piece.Bool;
    return true;
  }

  /// <summary>
  /// The slot a condition gives now: its result while the comparison holds,
  /// its default otherwise or while a compared value has no data.
  /// </summary>
  public static OverlayVariable Chosen(OverlayPiece condition, IOverlayNumberSource source) =>
    Holds(condition, source, 0) ? condition.Slots[OverlayPiece.Result] : condition.Slots[OverlayPiece.Default];

  /// <summary>
  /// Whether <paramref name="value"/> compares with <paramref name="than"/>
  /// by <paramref name="compare"/>: numbers the same to six decimals are the
  /// same, so 0.1 + 0.2 = 0.3.
  /// </summary>
  public static bool Holds(char compare, double value, double than)
  {
    double difference = value - than;
    bool same = Math.Abs(difference) <= 5e-7 * Math.Max(1, Math.Max(Math.Abs(value), Math.Abs(than)));
    return compare switch
    {
      '<' => !same && difference < 0,
      '≤' => same || difference < 0,
      '=' => same,
      '≠' => !same,
      '≥' => same || difference > 0,
      '>' => !same && difference > 0,
      _ => false,
    };
  }

  /// <summary>The number from <paramref name="start"/> to <paramref name="end"/>: one piece, or numbers joined by operators.</summary>
  public static bool TryRange(
    OverlayVariable variable,
    int start,
    int end,
    IOverlayNumberSource source,
    out double result,
    out bool isTime
  ) => Calculate(variable, start, end, source, 0, out result, out isTime);

  private static bool Holds(OverlayPiece condition, IOverlayNumberSource source, int depth)
  {
    OverlayVariable compare = condition.Slots[OverlayPiece.Compare];
    return compare.Count == 1
      && compare[0].Kind == OverlayPieceKind.Compare
      && TryNumber(condition.Slots[OverlayPiece.Left], source, depth + 1, out double left, out _)
      && TryNumber(condition.Slots[OverlayPiece.Right], source, depth + 1, out double right, out _)
      && Holds(compare[0].Op, left, right);
  }

  private static bool TryNumber(
    OverlayVariable variable,
    IOverlayNumberSource source,
    int depth,
    out double result,
    out bool isTime
  )
  {
    result = 0;
    isTime = false;
    if (variable == null || variable.Count == 0 || depth > MaxDepth)
      return false;
    // Pieces side by side give no one number.
    if (variable.SegmentEnd(0) != variable.Count)
      return false;
    return Calculate(variable, 0, variable.Count, source, depth, out result, out isTime);
  }

  // One piece that is of type <paramref name="type"/>, through conditions, groups and made values.
  private static bool TrySingle(
    OverlayVariable variable,
    IOverlayNumberSource source,
    int depth,
    OverlayValueType type,
    out OverlayPiece piece
  )
  {
    piece = null;
    if (variable == null || variable.Count != 1 || depth > MaxDepth)
      return false;
    OverlayPiece only = variable[0];
    switch (only.Kind)
    {
      case OverlayPieceKind.Color when type == OverlayValueType.Color:
      case OverlayPieceKind.Bool when type == OverlayValueType.Bool:
        piece = only;
        return true;
      case OverlayPieceKind.Value when type == OverlayValueType.Color && OverlayValueCatalog.Get(only.Value).IsColor:
        piece = only;
        return true;
      case OverlayPieceKind.Condition:
        return TrySingle(
          Holds(only, source, depth) ? only.Slots[OverlayPiece.Result] : only.Slots[OverlayPiece.Default],
          source,
          depth + 1,
          type,
          out piece
        );
      case OverlayPieceKind.Group:
        return TrySingle(only.Slots[0], source, depth + 1, type, out piece);
      case OverlayPieceKind.Custom:
        return OverlayCustomValues.TryGet(only.Text, out OverlayVariable made)
          && TrySingle(made, source, depth + 1, type, out piece);
      default:
        return false;
    }
  }

  private static bool Calculate(
    OverlayVariable variable,
    int start,
    int end,
    IOverlayNumberSource source,
    int depth,
    out double result,
    out bool isTime
  )
  {
    result = 0;
    isTime = false;
    if (end <= start)
      return false;
    double total = 0;
    double term = 0;
    int termTime = 0;
    bool hasTerm = false;
    bool anyTime = false;
    char addOperator = '+';
    char multiplyOperator = '\0';
    for (int i = start; i < end; i++)
    {
      OverlayPiece piece = variable[i];
      if (piece.Kind == OverlayPieceKind.Operator)
      {
        if (piece.Op == '*' || piece.Op == '/')
        {
          if (hasTerm)
            multiplyOperator = piece.Op;
          continue;
        }
        if (hasTerm)
          Close();
        addOperator = piece.Op;
        multiplyOperator = '\0';
        continue;
      }
      // A lone "-" still being typed counts as nothing yet.
      if (piece.Kind == OverlayPieceKind.Number && !piece.HasNumber)
        continue;
      if (!Operand(piece, source, depth, out double operand, out bool time))
        return false;
      if (!hasTerm || multiplyOperator == '\0')
      {
        if (hasTerm)
          Close();
        term = operand;
        termTime = time ? 1 : 0;
        hasTerm = true;
      }
      else if (multiplyOperator == '*')
      {
        term *= operand;
        termTime += time ? 1 : 0;
      }
      else
      {
        term /= operand;
        termTime -= time ? 1 : 0;
      }
      multiplyOperator = '\0';
    }
    if (hasTerm)
      Close();
    result = total;
    isTime = anyTime;
    return !double.IsNaN(result) && !double.IsInfinity(result);

    void Close()
    {
      total = addOperator == '-' ? total - term : total + term;
      if (termTime == 1)
        anyTime = true;
      hasTerm = false;
    }
  }

  private static bool Operand(
    OverlayPiece piece,
    IOverlayNumberSource source,
    int depth,
    out double number,
    out bool time
  )
  {
    number = 0;
    time = false;
    switch (piece.Kind)
    {
      case OverlayPieceKind.Number:
        number = piece.NumberValue;
        return true;
      case OverlayPieceKind.Value:
        OverlayValueDefinition value = OverlayValueCatalog.Get(piece.Value);
        if (!value.IsNumeric || source == null || !source.TryGetNumber(piece.Value, out number))
          return false;
        time = value.Kind == OverlayValueKind.Time;
        return true;
      case OverlayPieceKind.Condition:
        return TryNumber(
          Holds(piece, source, depth) ? piece.Slots[OverlayPiece.Result] : piece.Slots[OverlayPiece.Default],
          source,
          depth + 1,
          out number,
          out time
        );
      case OverlayPieceKind.Group:
        return TryNumber(piece.Slots[0], source, depth + 1, out number, out time);
      case OverlayPieceKind.Custom:
        return OverlayCustomValues.TryGet(piece.Text, out OverlayVariable made)
          && TryNumber(made, source, depth + 1, out number, out time);
      default:
        return false;
    }
  }
}
