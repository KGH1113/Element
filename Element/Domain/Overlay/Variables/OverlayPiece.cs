using System.Globalization;
using Element.Domain.Overlay.Values;

namespace Element.Domain.Overlay.Variables;

/// <summary>
/// One piece of a variable: something typed or picked (a number, words, a
/// color, true or false, a comparison), an operator, a live value, a
/// condition with its five slots, or a variable inside the variable. Pieces
/// are changed in place while they are edited; copy one with
/// <see cref="Clone"/> to keep it.
/// </summary>
internal sealed class OverlayPiece
{
  /// <summary>The slots of a condition, in order.</summary>
  public const int Left = 0,
    Compare = 1,
    Right = 2,
    Result = 3,
    Default = 4;

  private OverlayPiece(OverlayPieceKind kind) => Kind = kind;

  public OverlayPieceKind Kind { get; }

  /// <summary>A number as typed, words, or a color's #RRGGBB.</summary>
  public string Text { get; set; } = string.Empty;

  public OverlayValueId Value { get; set; }

  /// <summary>An operator (+ - * /) or a comparison (&lt; ≤ = ≠ ≥ &gt;).</summary>
  public char Op { get; set; }

  public bool Bool { get; set; }

  /// <summary>A condition's five slots, or a group's one; empty for the rest.</summary>
  public OverlayVariable[] Slots { get; private set; } = System.Array.Empty<OverlayVariable>();

  public static OverlayPiece NumberPiece(string typed) => new(OverlayPieceKind.Number) { Text = typed ?? "0" };

  public static OverlayPiece NumberPiece(double number) => NumberPiece(OverlayVariable.NumberText(number));

  public static OverlayPiece TextPiece(string words) => new(OverlayPieceKind.Text) { Text = words ?? string.Empty };

  public static OverlayPiece ColorPiece(string hex) =>
    new(OverlayPieceKind.Color) { Text = (hex ?? "#FFFFFF").ToUpperInvariant() };

  public static OverlayPiece BoolPiece(bool value) => new(OverlayPieceKind.Bool) { Bool = value };

  public static OverlayPiece ComparePiece(char op) => new(OverlayPieceKind.Compare) { Op = op };

  public static OverlayPiece OperatorPiece(char op) => new(OverlayPieceKind.Operator) { Op = op };

  public static OverlayPiece ValuePiece(OverlayValueId id) => new(OverlayPieceKind.Value) { Value = id };

  /// <summary>A new condition: "≥" picked, everything else empty.</summary>
  public static OverlayPiece ConditionPiece() =>
    new(OverlayPieceKind.Condition)
    {
      Slots = new[]
      {
        new OverlayVariable(),
        new OverlayVariable(ComparePiece('≥')),
        new OverlayVariable(),
        new OverlayVariable(),
        new OverlayVariable(),
      },
    };

  public static OverlayPiece ConditionPiece(
    OverlayVariable left,
    OverlayVariable compare,
    OverlayVariable right,
    OverlayVariable result,
    OverlayVariable fallback
  ) => new(OverlayPieceKind.Condition) { Slots = new[] { left, compare, right, result, fallback } };

  public static OverlayPiece GroupPiece(OverlayVariable inner) =>
    new(OverlayPieceKind.Group) { Slots = new[] { inner ?? new OverlayVariable() } };

  /// <summary>Something worked out: everything but an operator.</summary>
  public bool IsOperand => Kind != OverlayPieceKind.Operator;

  /// <summary>A number typed so far that counts: not empty, a lone "-" or ".".</summary>
  public bool HasNumber => Kind == OverlayPieceKind.Number && TryNumber(Text, out _);

  public double NumberValue => TryNumber(Text, out double number) ? number : 0;

  public OverlayPiece Clone()
  {
    OverlayPiece copy = new(Kind)
    {
      Text = Text,
      Value = Value,
      Op = Op,
      Bool = Bool,
    };
    if (Slots.Length > 0)
    {
      copy.Slots = new OverlayVariable[Slots.Length];
      for (int i = 0; i < Slots.Length; i++)
        copy.Slots[i] = Slots[i].Clone();
    }
    return copy;
  }

  public bool SameAs(OverlayPiece other)
  {
    if (
      other == null
      || Kind != other.Kind
      || Text != other.Text
      || Value != other.Value
      || Op != other.Op
      || Bool != other.Bool
    )
      return false;
    if (Slots.Length != other.Slots.Length)
      return false;
    for (int i = 0; i < Slots.Length; i++)
      if (!Slots[i].SameAs(other.Slots[i]))
        return false;
    return true;
  }

  internal static bool TryNumber(string text, out double number)
  {
    number = 0;
    if (string.IsNullOrEmpty(text) || text == "-" || text == ".")
      return false;
    return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out number)
      && !double.IsNaN(number)
      && !double.IsInfinity(number);
  }
}
