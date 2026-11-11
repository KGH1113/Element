using Element.Domain.Overlay.Values;

namespace Element.Domain.Overlay.Formula;

/// <summary>Works a formula out. Runs every frame a text shows one, so nothing here allocates.</summary>
internal static class OverlayFormulaEvaluator
{
  /// <summary>
  /// The result, with × ÷ before + −. <paramref name="isTime"/> is true when the
  /// result is a time (a time value not divided by another time). False when a
  /// value has no number, a word is used, or the result is not finite.
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
    if (formula.Count == 0 || formula.IsWords)
      return false;
    double total = 0;
    double term = 0;
    int termTime = 0;
    bool hasTerm = false;
    bool anyTime = false;
    char addOperator = '+';
    char multiplyOperator = '\0';
    for (int i = 0; i < formula.Count; i++)
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
      else
      {
        OverlayValueDefinition value = OverlayValueCatalog.Get(token.Value);
        if (!value.IsNumeric || source == null || !source.TryGetNumber(token.Value, out operand))
          return false;
        time = value.Kind == OverlayValueKind.Time ? 1 : 0;
      }
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
