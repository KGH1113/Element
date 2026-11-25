using Element.Domain.Overlay.Values;

namespace Element.Domain.Overlay.Formula;

/// <summary>
/// Editing a formula with the keypad: digits, operators, comparisons and
/// "then"/"otherwise" typed at the end, backspace, and values dropped in.
/// </summary>
/// <remarks>
/// A formula is one calculation, or one case or more: a calculation, a
/// comparison (&lt; ≤ = ≠ ≥ &gt;), what it is compared with, "then", and what
/// the case gives; "otherwise" starts the next case. The first case that
/// holds gives the result; when none does, the place the formula is in shows
/// what it had without one (its plain number or color). Typing only ever adds
/// at the end, so each key looks at the part the end is in
/// (<see cref="Part"/>). Words (the song title) and colors stand alone in
/// their part: they can't be calculated with or compared.
/// </remarks>
internal static class OverlayFormulaKeypad
{
  /// <summary>The key that adds "then" after a comparison, or "otherwise" after a case's result.</summary>
  public const char Next = '?';

  /// <summary>A color picked by hand starts as white.</summary>
  public const string NewColor = "#FFFFFF";

  /// <summary>One of the six comparisons: &lt; ≤ = ≠ ≥ &gt;.</summary>
  public static bool IsCompare(char key) => key is '<' or '≤' or '=' or '≠' or '≥' or '>';

  /// <summary>The part the formula's end is in, and where that part starts.</summary>
  public static OverlayFormulaPart Part(OverlayFormula formula, out int start)
  {
    for (int i = formula.Count - 1; i >= 0; i--)
      switch (formula[i].Kind)
      {
        case OverlayFormulaTokenKind.Compare:
          start = i + 1;
          return OverlayFormulaPart.Right;
        case OverlayFormulaTokenKind.Then:
          start = i + 1;
          return OverlayFormulaPart.Result;
        case OverlayFormulaTokenKind.Else:
          start = i + 1;
          return OverlayFormulaPart.Left;
      }
    start = 0;
    return OverlayFormulaPart.Left;
  }

  /// <summary>What <see cref="Next"/> would add now: "then", "otherwise", or nothing (null).</summary>
  public static OverlayFormulaTokenKind? NextStep(OverlayFormula formula)
  {
    OverlayFormulaPart part = Part(formula, out int start);
    if (!Complete(formula, start) || formula.Count >= OverlayFormula.MaxTokens)
      return null;
    if (part == OverlayFormulaPart.Right && !Alone(formula, start))
      return OverlayFormulaTokenKind.Then;
    if (part == OverlayFormulaPart.Result)
      return OverlayFormulaTokenKind.Else;
    return null;
  }

  /// <summary>Whether a keypad key (0–9 . + - * /, a comparison, or <see cref="Next"/>) would change the formula.</summary>
  public static bool CanType(OverlayFormula formula, char key, OverlayFormulaKind kind = OverlayFormulaKind.Number)
  {
    OverlayFormulaPart part = Part(formula, out int start);
    OverlayFormulaToken? last = formula.Last;
    bool empty = formula.Count == start;
    // A case's result in a color is one color, never a calculation.
    bool fixedPart = (kind == OverlayFormulaKind.Color && part == OverlayFormulaPart.Result) || Alone(formula, start);
    if (char.IsDigit(key) || key == '.')
    {
      if (fixedPart)
        return false;
      if (!empty && last is { Kind: OverlayFormulaTokenKind.Number } number)
        return key == '.'
          ? number.Number.IndexOf('.') < 0 && Digits(number.Number) < OverlayFormula.MaxDigits
          : Digits(number.Number) < OverlayFormula.MaxDigits;
      return (empty || last is { Kind: OverlayFormulaTokenKind.Operator }) && formula.Count < OverlayFormula.MaxTokens;
    }
    if (IsCompare(key))
    {
      // A comparison just typed can be swapped for another.
      if (last is { Kind: OverlayFormulaTokenKind.Compare } compare)
        return compare.Operator != key;
      return part == OverlayFormulaPart.Left
        && Complete(formula, start)
        && !Alone(formula, start)
        && formula.Count < OverlayFormula.MaxTokens;
    }
    if (key == Next)
      return NextStep(formula) != null;
    char op = OverlayFormulaSyntax.NormalizeOperator(key);
    if (op == '\0' || fixedPart)
      return false;
    if (empty)
      return op == '-' && formula.Count < OverlayFormula.MaxTokens;
    if (last is { Kind: OverlayFormulaTokenKind.Operator } previous)
      return previous.Operator != op;
    // A lone "-" needs its digits before an operator can follow.
    if (last is { Kind: OverlayFormulaTokenKind.Number } open && !open.HasNumber)
      return false;
    return formula.Count < OverlayFormula.MaxTokens;
  }

  /// <summary>Applies a keypad key; false when it does nothing here.</summary>
  public static bool Type(OverlayFormula formula, char key, OverlayFormulaKind kind = OverlayFormulaKind.Number)
  {
    if (!CanType(formula, key, kind))
      return false;
    Part(formula, out int start);
    OverlayFormulaToken? last = formula.Last;
    bool empty = formula.Count == start;
    if (char.IsDigit(key) || key == '.')
    {
      if (!empty && last is { Kind: OverlayFormulaTokenKind.Number } number)
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
    if (IsCompare(key))
    {
      if (last is { Kind: OverlayFormulaTokenKind.Compare })
        formula.SetLast(OverlayFormulaToken.CompareToken(key));
      else
        formula.Add(OverlayFormulaToken.CompareToken(key));
      return true;
    }
    if (key == Next)
    {
      formula.Add(
        NextStep(formula) == OverlayFormulaTokenKind.Then
          ? OverlayFormulaToken.ThenToken()
          : OverlayFormulaToken.ElseToken()
      );
      return true;
    }
    char op = OverlayFormulaSyntax.NormalizeOperator(key);
    if (empty)
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

  /// <summary>
  /// What a typed keyboard character means: &lt; or &gt; then = make ≤ or ≥,
  /// ! makes ≠, and Enter-free "then"/"otherwise" is ?. '\0' for none.
  /// </summary>
  public static char FromKeyboard(OverlayFormula formula, char c)
  {
    OverlayFormulaToken? last = formula?.Last;
    switch (c)
    {
      case '<':
      case '>':
        return c;
      case '!':
        return '≠';
      case '=':
        if (last is { Kind: OverlayFormulaTokenKind.Compare } compare)
          return compare.Operator == '<' ? '≤'
            : compare.Operator == '>' ? '≥'
            : '=';
        return '=';
      case '?':
        return Next;
      default:
        return '\0';
    }
  }

  /// <summary>Removes the last digit, or the last step.</summary>
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

  /// <summary>Whether <paramref name="id"/> joins the part being typed, after its trailing operator.</summary>
  public static bool CanJoin(OverlayFormula formula, OverlayValueId id)
  {
    Part(formula, out int start);
    return formula.Count > start
      && formula.Last is { Kind: OverlayFormulaTokenKind.Operator }
      && OverlayValueCatalog.Get(id).IsNumeric
      && !Alone(formula, start);
  }

  /// <summary>
  /// Whether <paramref name="id"/> may go in the part being typed of a
  /// formula that holds <paramref name="kind"/>. Numbers go where a
  /// calculation or a comparison does; words only in a text, never
  /// compared; colors only as a color's result (a color with no case is the
  /// color itself, which its owner sets).
  /// </summary>
  public static bool CanDrop(OverlayFormula formula, OverlayValueId id, OverlayFormulaKind kind)
  {
    OverlayValueDefinition value = OverlayValueCatalog.Get(id);
    OverlayFormulaPart part = Part(formula, out int start);
    bool joining = formula.Count > start && formula.Last is { Kind: OverlayFormulaTokenKind.Operator };
    if (value.IsColor)
      return kind == OverlayFormulaKind.Color && part == OverlayFormulaPart.Result && !joining;
    if (value.IsNumeric)
      return !(kind == OverlayFormulaKind.Color && part == OverlayFormulaPart.Result)
        && (!joining || !Alone(formula, start));
    return kind == OverlayFormulaKind.Text && part != OverlayFormulaPart.Right && !joining;
  }

  /// <summary>
  /// A dropped value joins after a trailing operator; otherwise it replaces
  /// what the part being typed holds. The custom color goes in as a color
  /// picked by hand, starting as <paramref name="customHex"/>.
  /// </summary>
  public static void Drop(OverlayFormula formula, OverlayValueId id, string customHex = NewColor)
  {
    if (CanJoin(formula, id))
    {
      formula.Add(OverlayFormulaToken.ValueToken(id));
      return;
    }
    Part(formula, out int start);
    while (formula.Count > start)
      formula.RemoveAt(formula.Count - 1);
    formula.Add(
      id == OverlayValueId.CustomColor
        ? OverlayFormulaToken.ColorToken(string.IsNullOrEmpty(customHex) ? NewColor : customHex)
        : OverlayFormulaToken.ValueToken(id)
    );
  }

  /// <summary>
  /// The formula without what is still being typed: a trailing operator, a
  /// lone "-", a comparison or "then" with nothing after it, and a case that
  /// has no result yet.
  /// </summary>
  public static OverlayFormula Trimmed(OverlayFormula formula)
  {
    OverlayFormula copy = formula.Clone();
    while (copy.Count > 0)
    {
      OverlayFormulaToken last = copy[copy.Count - 1];
      if (
        last.Kind is OverlayFormulaTokenKind.Operator or OverlayFormulaTokenKind.Else
        || (last.Kind == OverlayFormulaTokenKind.Number && !last.HasNumber)
      )
      {
        copy.RemoveAt(copy.Count - 1);
        continue;
      }
      // The last case is all or nothing: a comparison needs "then" and a result.
      int caseStart = -1;
      bool compare = false,
        then = false;
      for (int i = copy.Count - 1; i >= 0; i--)
      {
        OverlayFormulaTokenKind kind = copy[i].Kind;
        if (kind == OverlayFormulaTokenKind.Else)
        {
          caseStart = i;
          break;
        }
        compare |= kind == OverlayFormulaTokenKind.Compare;
        then |= kind == OverlayFormulaTokenKind.Then;
      }
      if (!(compare && (!then || last.Kind == OverlayFormulaTokenKind.Then)))
        return copy;
      while (copy.Count > System.Math.Max(0, caseStart))
        copy.RemoveAt(copy.Count - 1);
    }
    return copy;
  }

  // The part ends in something worked out: not an operator, and not a lone "-" or ".".
  private static bool Complete(OverlayFormula formula, int start)
  {
    if (formula.Count <= start)
      return false;
    OverlayFormulaToken last = formula[formula.Count - 1];
    return last.IsOperand && (last.Kind != OverlayFormulaTokenKind.Number || last.HasNumber);
  }

  // The part holds words or a color, which stand alone.
  private static bool Alone(OverlayFormula formula, int start)
  {
    for (int i = start; i < formula.Count; i++)
    {
      OverlayFormulaToken token = formula[i];
      if (token.Kind == OverlayFormulaTokenKind.Color)
        return true;
      if (token.Kind == OverlayFormulaTokenKind.Value && !OverlayValueCatalog.Get(token.Value).IsNumeric)
        return true;
    }
    return false;
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
