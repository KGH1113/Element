using System;
using System.Globalization;
using System.Text;
using Element.Domain.Overlay.Values;

namespace Element.Domain.Overlay.Formula;

/// <summary>
/// How a formula is written and saved. A formula that is one value saves as
/// that value's key, so texts from 1.1 read unchanged; anything else saves
/// values in braces: <c>{combo}*2</c>. A case saves as its comparison, ?
/// for "then" and ; for "otherwise": <c>{combo}&gt;=100?60;{miss}=0?40</c>;
/// a color picked by hand as #RRGGBB.
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
        if (OverlayValueCatalog.TryResolve(key, out OverlayValueDefinition value) && TakesOperand(formula))
          formula.Add(OverlayFormulaToken.ValueToken(value.Id));
        continue;
      }
      if (c == '#')
      {
        int length = 1;
        while (i + length < text.Length && length < 7 && Uri.IsHexDigit(text[i + length]))
          length++;
        if (length == 7 && TakesOperand(formula))
          formula.Add(OverlayFormulaToken.ColorToken(text.Substring(i, 7).ToUpperInvariant()));
        i += length - 1;
        continue;
      }
      bool sign = c == '-' && TakesOperand(formula);
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
        if (TakesOperand(formula) && (token.HasNumber || number == "-"))
          formula.Add(token);
        continue;
      }
      char compare = ReadCompare(text, ref i);
      if (compare != '\0')
      {
        OverlayFormulaKeypad.Type(formula, compare, OverlayFormulaKind.Text);
        continue;
      }
      if (c == '?' || c == ';')
      {
        // Only where it belongs: "then" after a comparison, "otherwise" after a result.
        OverlayFormulaTokenKind? next = OverlayFormulaKeypad.NextStep(formula);
        if (next == (c == '?' ? OverlayFormulaTokenKind.Then : OverlayFormulaTokenKind.Else))
          OverlayFormulaKeypad.Type(formula, OverlayFormulaKeypad.Next, OverlayFormulaKind.Text);
        continue;
      }
      char op = NormalizeOperator(c);
      if (op != '\0' && !TakesOperand(formula))
        formula.Add(OverlayFormulaToken.OperatorToken(op));
      else if (op != '\0' && formula.Count > 0 && formula.AwaitsOperand)
        formula.SetLast(OverlayFormulaToken.OperatorToken(op));
    }
    DropAloneInArithmetic(formula);
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
        case OverlayFormulaTokenKind.Compare:
          builder.Append(
            token.Operator switch
            {
              '≤' => "<=",
              '≥' => ">=",
              '≠' => "!=",
              _ => token.Operator.ToString(),
            }
          );
          break;
        case OverlayFormulaTokenKind.Then:
          builder.Append('?');
          break;
        case OverlayFormulaTokenKind.Else:
          builder.Append(';');
          break;
        case OverlayFormulaTokenKind.Color:
          builder.Append(token.Hex);
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

  // A number, a value or a color can come next: the part being typed is empty or ends in an operator.
  private static bool TakesOperand(OverlayFormula formula)
  {
    OverlayFormulaKeypad.Part(formula, out int start);
    return formula.Count == start || formula.AwaitsOperand;
  }

  // A comparison as saved (<, <=, =, !=, >=, >) or as shown (≤ ≠ ≥); '\0' for none.
  private static char ReadCompare(string text, ref int i)
  {
    char c = text[i];
    char after = i + 1 < text.Length ? text[i + 1] : '\0';
    switch (c)
    {
      case '≤':
      case '≥':
      case '≠':
        return c;
      case '<':
        if (after == '=')
        {
          i++;
          return '≤';
        }
        if (after == '>')
        {
          i++;
          return '≠';
        }
        return '<';
      case '>':
        if (after == '=')
        {
          i++;
          return '≥';
        }
        return '>';
      case '!':
        if (after == '=')
          i++;
        return '≠';
      case '=':
        if (after == '=')
          i++;
        return '=';
      default:
        return '\0';
    }
  }

  // Words and colors can't be calculated with: keep each only when it is the
  // whole of its part, and never in a comparison.
  private static void DropAloneInArithmetic(OverlayFormula formula)
  {
    if (formula.Count <= 1)
      return;
    for (int i = formula.Count - 1; i >= 0; i--)
    {
      OverlayFormulaToken token = formula[i];
      bool alone =
        token.Kind == OverlayFormulaTokenKind.Color
        || (token.Kind == OverlayFormulaTokenKind.Value && !OverlayValueCatalog.Get(token.Value).IsNumeric);
      if (!alone)
        continue;
      bool openBefore = i == 0 || formula[i - 1].IsSeparator;
      bool closeAfter = i == formula.Count - 1 || formula[i + 1].Kind == OverlayFormulaTokenKind.Else;
      bool result = formula.HasCases ? i > 0 && formula[i - 1].Kind == OverlayFormulaTokenKind.Then : true;
      if (openBefore && closeAfter && result)
        continue;
      formula.RemoveAt(i);
      if (i < formula.Count && formula[i].Kind == OverlayFormulaTokenKind.Operator)
        formula.RemoveAt(i);
      else if (i > 0 && formula[i - 1].Kind == OverlayFormulaTokenKind.Operator)
        formula.RemoveAt(i - 1);
    }
  }
}
