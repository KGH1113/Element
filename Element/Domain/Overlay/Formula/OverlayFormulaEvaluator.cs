using System;
using Element.Domain.Overlay.Values;

namespace Element.Domain.Overlay.Formula;

/// <summary>
/// Works a formula out. Runs every time a value a text shows changes, so
/// nothing here allocates. A formula with cases gives the result of the
/// first case that holds; with none holding it gives nothing, and the place
/// it is in shows what it had without one.
/// </summary>
internal static class OverlayFormulaEvaluator
{
  /// <summary>
  /// The result, with × ÷ before + −. <paramref name="isTime"/> is true when the
  /// result is a time (a time value not divided by another time). False when a
  /// value has no number, a word or a color is used, the result is not
  /// finite, or no case holds.
  /// </summary>
  public static bool TryEvaluate(
    OverlayFormula formula,
    IOverlayNumberSource source,
    out double result,
    out bool isTime
  )
  {
    result = 0;
    isTime = false;
    return TryPick(formula, source, out int start, out int end)
      && Calculate(formula, start, end, source, out result, out isTime);
  }

  /// <summary>
  /// Where the result is: the whole formula without cases, or the result of
  /// the first case that holds. False when there is nothing, or no case holds.
  /// A comparison with a value that has no data doesn't hold.
  /// </summary>
  public static bool TryPick(OverlayFormula formula, IOverlayNumberSource source, out int start, out int end)
  {
    start = end = 0;
    if (formula == null || formula.Count == 0)
      return false;
    if (!formula.HasCases)
    {
      end = formula.Count;
      return true;
    }
    int at = 0;
    while (at < formula.Count)
    {
      int compare = -1,
        then = -1,
        stop = formula.Count;
      for (int i = at; i < formula.Count; i++)
      {
        OverlayFormulaTokenKind kind = formula[i].Kind;
        if (kind == OverlayFormulaTokenKind.Else)
        {
          stop = i;
          break;
        }
        if (kind == OverlayFormulaTokenKind.Compare && compare < 0)
          compare = i;
        else if (kind == OverlayFormulaTokenKind.Then && then < 0)
          then = i;
      }
      if (
        compare > at
        && then > compare + 1
        && stop > then + 1
        && Calculate(formula, at, compare, source, out double left, out _)
        && Calculate(formula, compare + 1, then, source, out double right, out _)
        && Holds(formula[compare].Operator, left, right)
      )
      {
        start = then + 1;
        end = stop;
        return true;
      }
      at = stop + 1;
    }
    return false;
  }

  /// <summary>
  /// The color the first case that holds gives: one picked by hand, or a
  /// color value (the highlight color). False for a formula without cases,
  /// or when no case holds.
  /// </summary>
  public static bool TryPickColor(OverlayFormula formula, IOverlayNumberSource source, out OverlayFormulaToken color)
  {
    color = default;
    if (formula == null || !formula.HasCases || !TryPick(formula, source, out int start, out int end))
      return false;
    if (end - start != 1 || !formula[start].IsColor)
      return false;
    color = formula[start];
    return true;
  }

  /// <summary>
  /// How <paramref name="value"/> compares with <paramref name="than"/>:
  /// numbers the same to six decimals are the same, so 0.1 + 0.2 = 0.3.
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

  // The calculation from start to end.
  private static bool Calculate(
    OverlayFormula formula,
    int start,
    int end,
    IOverlayNumberSource source,
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
      OverlayFormulaToken token = formula[i];
      if (token.Kind == OverlayFormulaTokenKind.Operator)
      {
        if (token.Operator == '*' || token.Operator == '/')
        {
          if (hasTerm)
            multiplyOperator = token.Operator;
          continue;
        }
        if (hasTerm)
          Close();
        addOperator = token.Operator;
        multiplyOperator = '\0';
        continue;
      }
      double operand;
      int time = 0;
      if (token.Kind == OverlayFormulaTokenKind.Number)
      {
        // A lone "-" still being typed counts as nothing yet.
        if (!token.HasNumber)
          continue;
        operand = token.NumberValue;
      }
      else if (token.Kind == OverlayFormulaTokenKind.Value)
      {
        OverlayValueDefinition value = OverlayValueCatalog.Get(token.Value);
        if (!value.IsNumeric || source == null || !source.TryGetNumber(token.Value, out operand))
          return false;
        time = value.Kind == OverlayValueKind.Time ? 1 : 0;
      }
      else
        return false;
      if (!hasTerm || multiplyOperator == '\0')
      {
        if (hasTerm)
          Close();
        term = operand;
        termTime = time;
        hasTerm = true;
      }
      else if (multiplyOperator == '*')
      {
        term *= operand;
        termTime += time;
      }
      else
      {
        term /= operand;
        termTime -= time;
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
}
