namespace Element.Domain.Elements;

/// <summary>
/// Editing a formula with the keypad: digits and operators typed at the end,
/// backspace, and values dropped in.
/// </summary>
internal static class OverlayFormulaKeypad
{
  /// <summary>Whether a keypad key (0–9 . + - * /) would change the formula.</summary>
  public static bool CanType(OverlayFormula formula, char key)
  {
    if (formula.IsWords)
      return false;
    OverlayFormulaToken? last = formula.Last;
    if (char.IsDigit(key) || key == '.')
    {
      if (last is { Kind: OverlayFormulaTokenKind.Number } number)
        return key == '.'
          ? number.Number.IndexOf('.') < 0 && Digits(number.Number) < OverlayFormula.MaxDigits
          : Digits(number.Number) < OverlayFormula.MaxDigits;
      return last is not { Kind: OverlayFormulaTokenKind.Value } && formula.Count < OverlayFormula.MaxTokens;
    }
    char op = OverlayFormulaSyntax.NormalizeOperator(key);
    if (op == '\0')
      return false;
    if (formula.Count == 0)
      return op == '-';
    if (last is { Kind: OverlayFormulaTokenKind.Operator } previous)
      return previous.Operator != op;
    // A lone "-" needs its digits before an operator can follow.
    if (last is { Kind: OverlayFormulaTokenKind.Number } open && !open.HasNumber)
      return false;
    return formula.Count < OverlayFormula.MaxTokens;
  }

  /// <summary>Applies a keypad key; false when it does nothing here.</summary>
  public static bool Type(OverlayFormula formula, char key)
  {
    if (!CanType(formula, key))
      return false;
    OverlayFormulaToken? last = formula.Last;
    if (char.IsDigit(key) || key == '.')
    {
      if (last is { Kind: OverlayFormulaTokenKind.Number } number)
      {
        string text = number.Number;
        // "0" then "7" is "7", not "07".
        if (key != '.' && (text == "0" || text == "-0"))
          text = text.Substring(0, text.Length - 1);
        formula.SetLast(
          OverlayFormulaToken.NumberToken(text + (key == '.' && EndsWithSign(text) ? "0." : key.ToString()))
        );
      }
      else
        formula.Add(OverlayFormulaToken.NumberToken(key == '.' ? "0." : key.ToString()));
      return true;
    }
    char op = OverlayFormulaSyntax.NormalizeOperator(key);
    if (formula.Count == 0)
    {
      // A leading minus starts a negative number.
      formula.Add(OverlayFormulaToken.NumberToken("-"));
      return true;
    }
    if (last is { Kind: OverlayFormulaTokenKind.Operator })
      formula.SetLast(OverlayFormulaToken.OperatorToken(op));
    else
      formula.Add(OverlayFormulaToken.OperatorToken(op));
    return true;
  }

  /// <summary>Removes the last digit, or the last value or operator.</summary>
  public static bool Backspace(OverlayFormula formula)
  {
    if (formula.Count == 0)
      return false;
    OverlayFormulaToken last = formula[formula.Count - 1];
    if (last.Kind == OverlayFormulaTokenKind.Number && last.Number.Length > 1)
      formula.SetLast(OverlayFormulaToken.NumberToken(last.Number.Substring(0, last.Number.Length - 1)));
    else
      formula.RemoveAt(formula.Count - 1);
    return true;
  }

  /// <summary>Whether <paramref name="id"/> could be dropped here: words only replace, never join arithmetic.</summary>
  public static bool CanJoin(OverlayFormula formula, OverlayValueId id) =>
    formula.AwaitsOperand && OverlayValueCatalog.Get(id).IsNumeric && !formula.IsWords;

  /// <summary>
  /// A dropped value joins after a trailing operator; otherwise it replaces
  /// the whole formula.
  /// </summary>
  public static void Drop(OverlayFormula formula, OverlayValueId id)
  {
    if (CanJoin(formula, id))
    {
      formula.Add(OverlayFormulaToken.ValueToken(id));
      return;
    }
    formula.Clear();
    formula.Add(OverlayFormulaToken.ValueToken(id));
  }

  /// <summary>The formula without what is still being typed: a trailing operator or a lone "-".</summary>
  public static OverlayFormula Trimmed(OverlayFormula formula)
  {
    OverlayFormula copy = formula.Clone();
    while (copy.Count > 0)
    {
      OverlayFormulaToken last = copy[copy.Count - 1];
      if (
        last.Kind == OverlayFormulaTokenKind.Operator
        || (last.Kind == OverlayFormulaTokenKind.Number && !last.HasNumber)
      )
        copy.RemoveAt(copy.Count - 1);
      else
        break;
    }
    return copy;
  }

  private static bool EndsWithSign(string text) => text.Length == 0 || text[text.Length - 1] == '-';

  private static int Digits(string text)
  {
    int digits = 0;
    foreach (char c in text)
      if (char.IsDigit(c))
        digits++;
    return digits;
  }
}
