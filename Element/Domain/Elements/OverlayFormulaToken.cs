using System.Globalization;

namespace Element.Domain.Elements;

// One step of a formula: a number as typed ("12", "1.", "-3"), a value, or
// one of + − × ÷ (saved as + - * /). A number is read once, when the token is
// made, so working the formula out never parses its text again.
internal readonly struct OverlayFormulaToken
{
  public readonly OverlayFormulaTokenKind Kind;
  public readonly string Number;
  public readonly OverlayValueId Value;
  public readonly char Operator;

  /// <summary>What <see cref="Number"/> reads as, when <see cref="HasNumber"/>.</summary>
  public readonly double NumberValue;

  /// <summary>A number that counts: not empty, a lone "-" or "." still being typed.</summary>
  public readonly bool HasNumber;

  private OverlayFormulaToken(OverlayFormulaTokenKind kind, string number, OverlayValueId value, char op)
  {
    Kind = kind;
    Number = number;
    Value = value;
    Operator = op;
    double parsed = 0;
    HasNumber = kind == OverlayFormulaTokenKind.Number && TryNumber(number, out parsed);
    NumberValue = HasNumber ? parsed : 0;
  }

  public static OverlayFormulaToken NumberToken(string text) =>
    new(OverlayFormulaTokenKind.Number, text, default, '\0');

  public static OverlayFormulaToken ValueToken(OverlayValueId id) => new(OverlayFormulaTokenKind.Value, null, id, '\0');

  public static OverlayFormulaToken OperatorToken(char op) => new(OverlayFormulaTokenKind.Operator, null, default, op);

  public bool IsOperand => Kind != OverlayFormulaTokenKind.Operator;

  private static bool TryNumber(string text, out double number)
  {
    number = 0;
    if (string.IsNullOrEmpty(text) || text == "-" || text == ".")
      return false;
    return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out number)
      && !double.IsNaN(number)
      && !double.IsInfinity(number);
  }
}
