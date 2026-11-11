using System;
using System.Globalization;
using System.Text;
using Element.Domain.Overlay.Values;
using Element.Presentation.Overlay.Values;

namespace Element.Domain.Overlay.Formula;

/// <summary>
/// How a formula is written and saved. A formula that is one value saves as
/// that value's key, so texts from 1.1 read unchanged; anything else saves
/// values in braces: <c>{combo}*2</c>.
/// </summary>
internal static class OverlayFormulaSyntax
{
  public static OverlayFormula Parse(string text)
  {
    OverlayFormula formula = new();
    if (string.IsNullOrEmpty(text))
      return formula;
    text = text.Trim();
    if (OverlayValueCatalog.TryResolve(text, out OverlayValueDefinition single))
    {
      formula.Add(OverlayFormulaToken.ValueToken(single.Id));
      return formula;
    }
    for (int i = 0; i < text.Length && formula.Count < OverlayFormula.MaxTokens; i++)
    {
      char c = text[i];
      if (char.IsWhiteSpace(c))
        continue;
      if (c == '{')
      {
        int close = text.IndexOf('}', i + 1);
        if (close < 0)
          break;
        string key = text.Substring(i + 1, close - i - 1);
        i = close;
        // A built-in value that no longer exists is dropped with the operator
        // before it. A mod's value stays even while that mod is missing.
        if (OverlayValueCatalog.TryResolve(key, out OverlayValueDefinition value) && formula.CanTakeOperand)
          formula.Add(OverlayFormulaToken.ValueToken(value.Id));
        continue;
      }
      bool sign = c == '-' && formula.CanTakeOperand;
      if (char.IsDigit(c) || c == '.' || sign)
      {
        int start = i;
        if (sign)
          i++;
        while (i < text.Length && (char.IsDigit(text[i]) || text[i] == '.'))
          i++;
        string number = text.Substring(start, i - start);
        i--;
        OverlayFormulaToken token = OverlayFormulaToken.NumberToken(number);
        if (formula.CanTakeOperand && (token.HasNumber || number == "-"))
          formula.Add(token);
        continue;
      }
      char op = NormalizeOperator(c);
      if (op != '\0' && formula.Count > 0)
      {
        if (formula.AwaitsOperand)
          formula.SetLast(OverlayFormulaToken.OperatorToken(op));
        else
          formula.Add(OverlayFormulaToken.OperatorToken(op));
      }
    }
    DropWordsInArithmetic(formula);
    return formula;
  }

  public static string Serialize(OverlayFormula formula)
  {
    if (formula.IsSingleValue(out OverlayValueId only))
      return OverlayValueCatalog.Get(only).Key;
    StringBuilder builder = new();
    for (int i = 0; i < formula.Count; i++)
    {
      OverlayFormulaToken token = formula[i];
      switch (token.Kind)
      {
        case OverlayFormulaTokenKind.Number:
          builder.Append(token.Number);
          break;
        case OverlayFormulaTokenKind.Value:
          builder.Append('{').Append(OverlayValueCatalog.Get(token.Value).Key).Append('}');
          break;
        default:
          builder.Append(token.Operator);
          break;
      }
    }
    return builder.ToString();
  }

  /// <summary>+ - * / and their display forms (− × ÷ x) become + - * /.</summary>
  public static char NormalizeOperator(char c) =>
    c switch
    {
      '+' => '+',
      '-' or '−' => '-',
      '*' or '×' or 'x' or 'X' => '*',
      '/' or '÷' => '/',
      _ => '\0',
    };

  public static string NumberText(double number)
  {
    if (double.IsNaN(number) || double.IsInfinity(number))
      return "0";
    return Math.Round(number, 2).ToString("0.##", CultureInfo.InvariantCulture);
  }

  // A word can't be calculated with: keep it only when it is the whole formula.
  private static void DropWordsInArithmetic(OverlayFormula formula)
  {
    if (formula.Count <= 1)
      return;
    for (int i = formula.Count - 1; i >= 0; i--)
      if (formula[i].Kind == OverlayFormulaTokenKind.Value && !OverlayValueCatalog.Get(formula[i].Value).IsNumeric)
      {
        formula.RemoveAt(i);
        if (i < formula.Count && formula[i].Kind == OverlayFormulaTokenKind.Operator)
          formula.RemoveAt(i);
        else if (i > 0 && formula[i - 1].Kind == OverlayFormulaTokenKind.Operator)
          formula.RemoveAt(i - 1);
      }
  }
}
