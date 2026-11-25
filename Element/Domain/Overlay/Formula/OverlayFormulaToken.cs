using System.Globalization;
using Element.Domain.Overlay.Values;

namespace Element.Domain.Overlay.Formula;

// One step of a formula: a number as typed ("12", "1.", "-3"), a value, one
// of + − × ÷ (saved as + - * /), a comparison (< ≤ = ≠ ≥ >), "then",
// "otherwise", or a color picked by hand (#RRGGBB). A number is read once,
// when the token is made, so working the formula out never parses its text
// again.
internal readonly struct OverlayFormulaToken
{
  public readonly OverlayFormulaTokenKind Kind;
  public readonly string Number;
  public readonly OverlayValueId Value;
  public readonly char Operator;

  /// <summary>A hand-picked color as #RRGGBB; null for any other step.</summary>
  public readonly string Hex;

  /// <summary>What <see cref="Number"/> reads as, when <see cref="HasNumber"/>.</summary>
  public readonly double NumberValue;

  /// <summary>A number that counts: not empty, a lone "-" or "." still being typed.</summary>
  public readonly bool HasNumber;

  private OverlayFormulaToken(
    OverlayFormulaTokenKind kind,
    string number,
    OverlayValueId value,
    char op,
    string hex = null
  )
  {
    Kind = kind;
    Number = number;
    Value = value;
    Operator = op;
    Hex = hex;
    double parsed = 0;
    HasNumber = kind == OverlayFormulaTokenKind.Number && TryNumber(number, out parsed);
    NumberValue = HasNumber ? parsed : 0;
  }

  public static OverlayFormulaToken NumberToken(string text) =>
    new(OverlayFormulaTokenKind.Number, text, default, '\0');

  public static OverlayFormulaToken ValueToken(OverlayValueId id) => new(OverlayFormulaTokenKind.Value, null, id, '\0');

  public static OverlayFormulaToken OperatorToken(char op) => new(OverlayFormulaTokenKind.Operator, null, default, op);

  /// <summary>One of &lt; ≤ = ≠ ≥ &gt;.</summary>
  public static OverlayFormulaToken CompareToken(char op) => new(OverlayFormulaTokenKind.Compare, null, default, op);

  public static OverlayFormulaToken ThenToken() => new(OverlayFormulaTokenKind.Then, null, default, '\0');

  public static OverlayFormulaToken ElseToken() => new(OverlayFormulaTokenKind.Else, null, default, '\0');

  /// <summary>A color picked by hand, as #RRGGBB.</summary>
  public static OverlayFormulaToken ColorToken(string hex) =>
    new(OverlayFormulaTokenKind.Color, null, default, '\0', hex);

  /// <summary>A number, a value or a color: something worked out, not something between them.</summary>
  public bool IsOperand =>
    Kind is OverlayFormulaTokenKind.Number or OverlayFormulaTokenKind.Value or OverlayFormulaTokenKind.Color;

  /// <summary>A step that starts a new part of a case: a comparison, "then" or "otherwise".</summary>
  public bool IsSeparator =>
    Kind is OverlayFormulaTokenKind.Compare or OverlayFormulaTokenKind.Then or OverlayFormulaTokenKind.Else;

  /// <summary>A color: one picked by hand, or a color value such as the highlight color.</summary>
  public bool IsColor =>
    Kind == OverlayFormulaTokenKind.Color
    || (Kind == OverlayFormulaTokenKind.Value && OverlayValueCatalog.Get(Value).IsColor);

  public bool Same(OverlayFormulaToken other) =>
    Kind == other.Kind
    && Number == other.Number
    && Value == other.Value
    && Operator == other.Operator
    && Hex == other.Hex;

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
