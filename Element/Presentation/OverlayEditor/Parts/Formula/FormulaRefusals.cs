using Element.Domain.Overlay.Values;
using Element.Domain.Overlay.Variables;
using ElementUI;
using static Element.Presentation.Ui.Theme.UiLocale;

namespace Element.Presentation.OverlayEditor.Parts.Formula;

/// <summary>The names the dock gives types and a condition's slots.</summary>
internal static class FormulaRefusals
{
  /// <summary>A type as the dock names it: 숫자, 글자, 색 and so on.</summary>
  public static string TypeName(OverlayValueType type) =>
    type switch
    {
      OverlayValueType.Number => L("숫자", "a number"),
      OverlayValueType.Text => L("글자", "words"),
      OverlayValueType.Color => L("색", "a color"),
      OverlayValueType.Bool => L("참·거짓", "true or false"),
      OverlayValueType.Compare => L("비교 기호", "a comparison"),
      OverlayValueType.Point => L("자리", "a point"),
      _ => L("아무거나", "anything"),
    };

  /// <summary>A condition's slot as the dock names it.</summary>
  public static string SlotName(int slot) =>
    slot switch
    {
      OverlayPiece.Left => L("비교할 값", "Compared"),
      OverlayPiece.Compare => L("비교 기호", "Comparison"),
      OverlayPiece.Right => L("비교 당할 값", "Compared with"),
      OverlayPiece.Result => L("결과값", "Result"),
      OverlayPiece.Default => L("기본값", "Default"),
      _ => L("묶음", "Group"),
    };
}
