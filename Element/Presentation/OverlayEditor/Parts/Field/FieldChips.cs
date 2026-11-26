using System.Collections.Generic;
using Element.Configuration.Overlay;
using Element.Domain.Overlay.Values;
using Element.Domain.Overlay.Variables;
using Element.Presentation.Overlay.Elements;
using Element.Presentation.Overlay.Text;
using Element.Presentation.OverlayEditor.Parts.Dock.Line;
using Element.Presentation.Ui.Theme;
using UnityEngine;
using static Element.Presentation.Ui.Theme.UiLocale;

namespace Element.Presentation.OverlayEditor.Parts.Field;

/// <summary>
/// What a field row shows of a variable: its first pieces as chips (a
/// condition as one chip, a long text cut short), "…" past three, and
/// "비어 있음" for an empty one.
/// </summary>
internal static class FieldChips
{
  private const int MostShown = 3;

  /// <param name="around">What the field gives: a condition or a calculation in it gives that too.</param>
  /// <param name="most">How many pieces show before "…"; a narrow place shows fewer.</param>
  public static FieldChip[] Of(
    OverlayVariable variable,
    OverlayValueType around = OverlayValueType.Any,
    int most = MostShown
  )
  {
    if (variable == null || variable.IsEmpty)
      return new[] { new FieldChip("empty", L("비어 있음", "Empty"), OverlayValueType.Any) { Muted = true } };
    List<FieldChip> chips = new(most + 1);
    for (int i = 0; i < variable.Count && i < most; i++)
      chips.Add(Of(variable[i], i, around));
    if (variable.Count > most)
      chips.Add(new FieldChip("more", "…", OverlayValueType.Any) { Operator = true });
    return chips.ToArray();
  }

  /// <summary>
  /// What a color field holds: its value or condition when it has one, else
  /// one chip for the gradient (drawn as <paramref name="gradient"/>), the
  /// highlight color or the custom color, in <paramref name="shown"/>.
  /// </summary>
  public static FieldChip[] Of(OverlayColorSettings color, Color shown, OverlayResolvedGradient gradient)
  {
    if (color == null)
      return new[] { new FieldChip("mixed", L("여러 개", "Mixed"), OverlayValueType.Color) { Muted = true } };
    if (!string.IsNullOrEmpty(color.Formula))
      return Of(OverlayVariable.Parse(color.Formula), OverlayValueType.Color);
    shown.a = 1;
    if (color.IsGradient)
      return new[]
      {
        new FieldChip("gradient", L("그라데이션", "Gradient"), OverlayValueType.Color)
        {
          HasSwatch = true,
          Swatch = shown,
          Gradient = gradient,
        },
      };
    if (color.Highlight)
      return new[]
      {
        new FieldChip("highlight", L("하이라이트 색", "Highlight color"), OverlayValueType.Color)
        {
          HasSwatch = true,
          Swatch = shown,
        },
      };
    string hex = string.IsNullOrEmpty(color.Hex) ? "#FFFFFF" : color.Hex.ToUpperInvariant();
    return new[]
    {
      new FieldChip("custom", hex[0] == '#' ? hex : "#" + hex, OverlayValueType.Color)
      {
        HasSwatch = true,
        Swatch = shown,
      },
    };
  }

  private static FieldChip Of(OverlayPiece piece, int index, OverlayValueType around)
  {
    string key = index + ":" + (int)piece.Kind;
    OverlayValueType type = OverlayVariableTypes.TypeOf(piece);
    switch (piece.Kind)
    {
      case OverlayPieceKind.Operator:
        return new FieldChip(key, LineViewModel.Label(piece), OverlayValueType.Number) { Operator = true };
      case OverlayPieceKind.Condition:
        return new FieldChip(key, L("조건", "Condition"), around);
      case OverlayPieceKind.Group:
        return new FieldChip(key, L("계산", "Calculation"), around);
      case OverlayPieceKind.Text:
        string words = piece.Text.Length > 8 ? piece.Text.Substring(0, 7) + "…" : piece.Text;
        return new FieldChip(key, words, type);
      case OverlayPieceKind.Color:
        return new FieldChip(key, piece.Text, OverlayValueType.Color)
        {
          HasSwatch = true,
          Swatch = OverlayTextComposer.ParseHex(piece.Text),
        };
      case OverlayPieceKind.Value when OverlayValueCatalog.Get(piece.Value).IsColor:
        return new FieldChip(key, LineViewModel.Label(piece), OverlayValueType.Color)
        {
          HasSwatch = true,
          Swatch = UiPalette.Accent,
        };
      default:
        return new FieldChip(key, LineViewModel.Label(piece), type);
    }
  }
}
