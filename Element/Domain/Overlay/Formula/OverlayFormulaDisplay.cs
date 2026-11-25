using System;
using System.Text;
using Element.Domain.Overlay.Values;

namespace Element.Domain.Overlay.Formula;

/// <summary>How a formula reads to the player: − × ÷ instead of - * /, values by name.</summary>
internal static class OverlayFormulaDisplay
{
  /// <summary>What "then" and "otherwise" read as when the caller names no words of its own.</summary>
  public const string Then = "→",
    Otherwise = "·";

  /// <summary>
  /// Readable form for tooltips: <c>Combo × 2</c>, or
  /// <c>Combo ≥ 100 then 60 otherwise ...</c> with the caller's words.
  /// </summary>
  public static string Describe(
    OverlayFormula formula,
    Func<OverlayValueDefinition, string> name,
    string then = Then,
    string otherwise = Otherwise
  )
  {
    StringBuilder builder = new();
    for (int i = 0; i < formula.Count; i++)
    {
      OverlayFormulaToken token = formula[i];
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
        case OverlayFormulaTokenKind.Then:
          builder.Append(then);
          break;
        case OverlayFormulaTokenKind.Else:
          builder.Append(otherwise);
          break;
        case OverlayFormulaTokenKind.Color:
          builder.Append(token.Hex);
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
}
