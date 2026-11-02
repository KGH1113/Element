using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Element.Domain.Elements;

internal enum OverlayFormulaTokenKind
{
  Number,
  Value,
  Operator,
}

// One step of a formula: a number as typed ("12", "1.", "-3"), a value, or
// one of + − × ÷ (saved as + - * /).
internal readonly struct OverlayFormulaToken
{
  public readonly OverlayFormulaTokenKind Kind;
  public readonly string Number;
  public readonly OverlayValueId Value;
  public readonly char Operator;

  private OverlayFormulaToken(OverlayFormulaTokenKind kind, string number, OverlayValueId value, char op)
  {
    Kind = kind;
    Number = number;
    Value = value;
    Operator = op;
  }

  public static OverlayFormulaToken NumberToken(string text) =>
    new(OverlayFormulaTokenKind.Number, text, default, '\0');

  public static OverlayFormulaToken ValueToken(OverlayValueId id) => new(OverlayFormulaTokenKind.Value, null, id, '\0');

  public static OverlayFormulaToken OperatorToken(char op) => new(OverlayFormulaTokenKind.Operator, null, default, op);

  public bool IsOperand => Kind != OverlayFormulaTokenKind.Operator;
}

/// <summary>Numbers for values in a formula; false when the game has none right now.</summary>
internal interface IOverlayNumberSource
{
  bool TryGetNumber(OverlayValueId id, out double number);
}

/// <summary>
/// Numbers and values joined by + − × ÷, with × and ÷ first. A formula that is
/// one value saves as that value's key, so texts from 1.1 read unchanged;
/// anything else saves values in braces: <c>{combo}*2</c>. Words such as the
/// song title can only stand alone.
/// </summary>
internal sealed class OverlayFormula
{
  public const int MaxTokens = 31;
  public const int MaxDigits = 9;
  private readonly List<OverlayFormulaToken> _tokens = new();

  public int Count => _tokens.Count;
  public OverlayFormulaToken this[int index] => _tokens[index];
  public bool IsEmpty => _tokens.Count == 0;

  /// <summary>The last step is an operator, so the next number or value joins the formula.</summary>
  public bool AwaitsOperand => _tokens.Count > 0 && _tokens[_tokens.Count - 1].Kind == OverlayFormulaTokenKind.Operator;

  public static OverlayFormula Parse(string text)
  {
    OverlayFormula formula = new();
    if (string.IsNullOrEmpty(text))
      return formula;
    text = text.Trim();
    if (OverlayValueCatalog.TryGet(text, out OverlayValueDefinition single))
    {
      formula._tokens.Add(OverlayFormulaToken.ValueToken(single.Id));
      return formula;
    }
    for (int i = 0; i < text.Length && formula._tokens.Count < MaxTokens; i++)
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
        // A value that no longer exists is dropped with the operator before it.
        if (OverlayValueCatalog.TryGet(key, out OverlayValueDefinition value) && formula.CanTakeOperand)
          formula._tokens.Add(OverlayFormulaToken.ValueToken(value.Id));
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
        if (formula.CanTakeOperand && IsNumberText(number))
          formula._tokens.Add(OverlayFormulaToken.NumberToken(number));
        continue;
      }
      char op = NormalizeOperator(c);
      if (op != '\0' && formula._tokens.Count > 0)
      {
        if (formula.AwaitsOperand)
          formula._tokens[formula._tokens.Count - 1] = OverlayFormulaToken.OperatorToken(op);
        else
          formula._tokens.Add(OverlayFormulaToken.OperatorToken(op));
      }
    }
    formula.DropWordsInArithmetic();
    return formula;
  }

  public static OverlayFormula FromValue(OverlayValueId id)
  {
    OverlayFormula formula = new();
    formula._tokens.Add(OverlayFormulaToken.ValueToken(id));
    return formula;
  }

  public static OverlayFormula FromNumber(double number)
  {
    OverlayFormula formula = new();
    formula._tokens.Add(OverlayFormulaToken.NumberToken(NumberText(number)));
    return formula;
  }

  public OverlayFormula Clone()
  {
    OverlayFormula copy = new();
    copy._tokens.AddRange(_tokens);
    return copy;
  }

  public string Serialize()
  {
    if (IsSingleValue(out OverlayValueId only))
      return OverlayValueCatalog.Get(only).Key;
    StringBuilder builder = new();
    foreach (OverlayFormulaToken token in _tokens)
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
    return builder.ToString();
  }

  public bool IsSingleValue(out OverlayValueId id)
  {
    id = default;
    if (_tokens.Count != 1 || _tokens[0].Kind != OverlayFormulaTokenKind.Value)
      return false;
    id = _tokens[0].Value;
    return true;
  }

  /// <summary>One plain number: a field holding it keeps the number, not a formula.</summary>
  public bool IsPlainNumber(out double number)
  {
    number = 0;
    return _tokens.Count == 1
      && _tokens[0].Kind == OverlayFormulaTokenKind.Number
      && TryNumber(_tokens[0].Number, out number);
  }

  public bool HasValues
  {
    get
    {
      foreach (OverlayFormulaToken token in _tokens)
        if (token.Kind == OverlayFormulaTokenKind.Value)
          return true;
      return false;
    }
  }

  /// <summary>Words (the song title, a label) stand alone; arithmetic can't use them.</summary>
  public bool IsWords => IsSingleValue(out OverlayValueId id) && !OverlayValueCatalog.Get(id).IsNumeric;

  public ulong Mask
  {
    get
    {
      ulong mask = 0;
      foreach (OverlayFormulaToken token in _tokens)
        if (token.Kind == OverlayFormulaTokenKind.Value)
          mask |= OverlayValueCatalog.Bit(token.Value);
      return mask;
    }
  }

  // ---- Keypad editing ----

  /// <summary>Whether a keypad key (0–9 . + - * /) would change the formula.</summary>
  public bool CanType(char key)
  {
    if (IsWords)
      return false;
    OverlayFormulaToken? last = _tokens.Count > 0 ? _tokens[_tokens.Count - 1] : null;
    if (char.IsDigit(key) || key == '.')
    {
      if (last is { Kind: OverlayFormulaTokenKind.Number } number)
        return key == '.'
          ? number.Number.IndexOf('.') < 0 && Digits(number.Number) < MaxDigits
          : Digits(number.Number) < MaxDigits;
      return last is not { Kind: OverlayFormulaTokenKind.Value } && _tokens.Count < MaxTokens;
    }
    char op = NormalizeOperator(key);
    if (op == '\0')
      return false;
    if (_tokens.Count == 0)
      return op == '-';
    if (last is { Kind: OverlayFormulaTokenKind.Operator } previous)
      return previous.Operator != op;
    // A lone "-" needs its digits before an operator can follow.
    if (last is { Kind: OverlayFormulaTokenKind.Number } open && !TryNumber(open.Number, out _))
      return false;
    return _tokens.Count < MaxTokens;
  }

  /// <summary>Applies a keypad key; false when it does nothing here.</summary>
  public bool Type(char key)
  {
    if (!CanType(key))
      return false;
    int lastIndex = _tokens.Count - 1;
    OverlayFormulaToken? last = lastIndex >= 0 ? _tokens[lastIndex] : null;
    if (char.IsDigit(key) || key == '.')
    {
      if (last is { Kind: OverlayFormulaTokenKind.Number } number)
      {
        string text = number.Number;
        // "0" then "7" is "7", not "07".
        if (key != '.' && (text == "0" || text == "-0"))
          text = text.Substring(0, text.Length - 1);
        _tokens[lastIndex] = OverlayFormulaToken.NumberToken(
          text + (key == '.' && EndsWithSign(text) ? "0." : key.ToString())
        );
      }
      else
        _tokens.Add(OverlayFormulaToken.NumberToken(key == '.' ? "0." : key.ToString()));
      return true;
    }
    char op = NormalizeOperator(key);
    if (_tokens.Count == 0)
    {
      // A leading minus starts a negative number.
      _tokens.Add(OverlayFormulaToken.NumberToken("-"));
      return true;
    }
    if (last is { Kind: OverlayFormulaTokenKind.Operator })
      _tokens[lastIndex] = OverlayFormulaToken.OperatorToken(op);
    else
      _tokens.Add(OverlayFormulaToken.OperatorToken(op));
    return true;
  }

  public bool CanBackspace => _tokens.Count > 0;

  /// <summary>Removes the last digit, or the last value or operator.</summary>
  public bool Backspace()
  {
    if (_tokens.Count == 0)
      return false;
    int lastIndex = _tokens.Count - 1;
    OverlayFormulaToken last = _tokens[lastIndex];
    if (last.Kind == OverlayFormulaTokenKind.Number && last.Number.Length > 1)
      _tokens[lastIndex] = OverlayFormulaToken.NumberToken(last.Number.Substring(0, last.Number.Length - 1));
    else
      _tokens.RemoveAt(lastIndex);
    return true;
  }

  /// <summary>Whether <paramref name="id"/> could be dropped here: words only replace, never join arithmetic.</summary>
  public bool CanJoin(OverlayValueId id) => AwaitsOperand && OverlayValueCatalog.Get(id).IsNumeric && !IsWords;

  /// <summary>
  /// A dropped value joins after a trailing operator; otherwise it replaces
  /// the whole formula.
  /// </summary>
  public void Drop(OverlayValueId id)
  {
    if (CanJoin(id))
    {
      _tokens.Add(OverlayFormulaToken.ValueToken(id));
      return;
    }
    _tokens.Clear();
    _tokens.Add(OverlayFormulaToken.ValueToken(id));
  }

  public void Clear() => _tokens.Clear();

  /// <summary>The formula without what is still being typed: a trailing operator or a lone "-".</summary>
  public OverlayFormula Trimmed()
  {
    OverlayFormula copy = Clone();
    while (copy._tokens.Count > 0)
    {
      OverlayFormulaToken last = copy._tokens[copy._tokens.Count - 1];
      if (
        last.Kind == OverlayFormulaTokenKind.Operator
        || (last.Kind == OverlayFormulaTokenKind.Number && !TryNumber(last.Number, out _))
      )
        copy._tokens.RemoveAt(copy._tokens.Count - 1);
      else
        break;
    }
    return copy;
  }

  public bool SameAs(OverlayFormula other)
  {
    if (other == null || other._tokens.Count != _tokens.Count)
      return false;
    for (int i = 0; i < _tokens.Count; i++)
    {
      OverlayFormulaToken a = _tokens[i],
        b = other._tokens[i];
      if (a.Kind != b.Kind || a.Number != b.Number || a.Value != b.Value || a.Operator != b.Operator)
        return false;
    }
    return true;
  }

  // ---- Evaluation ----

  /// <summary>
  /// The result, with × ÷ before + −. <paramref name="isTime"/> is true when the
  /// result is a time (a time value not divided by another time). False when a
  /// value has no number, a word is used, or the result is not finite.
  /// </summary>
  public bool TryEvaluate(IOverlayNumberSource source, out double result, out bool isTime)
  {
    result = 0;
    isTime = false;
    if (_tokens.Count == 0 || IsWords)
      return false;
    double total = 0;
    double term = 0;
    int termTime = 0;
    bool hasTerm = false;
    bool anyTime = false;
    char addOperator = '+';
    char multiplyOperator = '\0';
    foreach (OverlayFormulaToken token in _tokens)
    {
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
        if (!TryNumber(token.Number, out operand))
          continue;
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

  /// <summary>Readable form for the keypad display and tooltips: <c>Combo × 2</c>.</summary>
  public string Describe(Func<OverlayValueDefinition, string> name)
  {
    StringBuilder builder = new();
    foreach (OverlayFormulaToken token in _tokens)
    {
      if (builder.Length > 0)
        builder.Append(' ');
      switch (token.Kind)
      {
        case OverlayFormulaTokenKind.Number:
          builder.Append(token.Number.Replace('-', '−'));
          break;
        case OverlayFormulaTokenKind.Value:
          builder.Append(name(OverlayValueCatalog.Get(token.Value)));
          break;
        default:
          builder.Append(Symbol(token.Operator));
          break;
      }
    }
    return builder.ToString();
  }

  public static char Symbol(char op) =>
    op switch
    {
      '+' => '+',
      '-' => '−',
      '*' => '×',
      '/' => '÷',
      _ => op,
    };

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

  private bool CanTakeOperand => _tokens.Count == 0 || AwaitsOperand;

  // A word can't be calculated with: keep it only when it is the whole formula.
  private void DropWordsInArithmetic()
  {
    if (_tokens.Count <= 1)
      return;
    for (int i = _tokens.Count - 1; i >= 0; i--)
      if (_tokens[i].Kind == OverlayFormulaTokenKind.Value && !OverlayValueCatalog.Get(_tokens[i].Value).IsNumeric)
      {
        _tokens.RemoveAt(i);
        if (i < _tokens.Count && _tokens[i].Kind == OverlayFormulaTokenKind.Operator)
          _tokens.RemoveAt(i);
        else if (i > 0 && _tokens[i - 1].Kind == OverlayFormulaTokenKind.Operator)
          _tokens.RemoveAt(i - 1);
      }
  }

  private static bool IsNumberText(string text) => TryNumber(text, out _) || text == "-";

  private static bool TryNumber(string text, out double number)
  {
    number = 0;
    if (string.IsNullOrEmpty(text) || text == "-" || text == ".")
      return false;
    return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out number)
      && !double.IsNaN(number)
      && !double.IsInfinity(number);
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
