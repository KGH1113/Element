using System;
using System.Text;
using Element.Domain.Overlay.Values;

namespace Element.Domain.Overlay.Formula;

/// <summary>How a formula reads to the player: − × ÷ instead of - * /, values by name.</summary>
internal static class OverlayFormulaDisplay
{
  /// <summary>Readable form for the keypad display and tooltips: <c>Combo × 2</c>.</summary>
  public static string Describe(OverlayFormula formula, Func<OverlayValueDefinition, string> name)
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
