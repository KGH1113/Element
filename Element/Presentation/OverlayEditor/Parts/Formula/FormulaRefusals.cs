using Element.Domain.Overlay.Formula;
using Element.Domain.Overlay.Values;
using static Element.Presentation.Ui.Theme.UiLocale;

namespace Element.Presentation.OverlayEditor.Parts.Formula;

/// <summary>Why a value can't go where it was put, in words the player can act on.</summary>
internal static class FormulaRefusals
{
  /// <summary>
  /// Why <paramref name="value"/> can't go in what <paramref name="editing"/>
  /// types, or in a number field when nothing is typed.
  /// </summary>
  public static string Why(OverlayValueDefinition value, FormulaEditing editing)
  {
    string name = ValueSectionNames.ValueName(value);
    FormulaSlot slot = editing?.Active == true ? editing.Slot : null;
    OverlayFormulaKind kind = slot?.Kind ?? OverlayFormulaKind.Number;
    OverlayFormulaPart part =
      slot != null ? OverlayFormulaKeypad.Part(editing.Working, out _) : OverlayFormulaPart.Left;
    if (value.IsColor)
      return kind == OverlayFormulaKind.Color
        ? L(
          "색은 '이면' 다음에 와요. 조건을 먼저 적어 주세요.",
          "A color comes after 'then'. Write the condition first."
        )
        : L(
          name + "은(는) 색이라서 숫자 칸에는 쓸 수 없어요. 글자 색이나 채우기에서 써 주세요.",
          name + " is a color, so it can't go in a number. Use it in a text color or a fill."
        );
    if (kind == OverlayFormulaKind.Color && part == OverlayFormulaPart.Result)
      return L(
        "'이면' 다음에는 색이 와요. 아래 '색'에서 하이라이트 색이나 커스텀 색을 골라 주세요.",
        "A color comes after 'then'. Pick the highlight color or a custom color under Colors."
      );
    if (part == OverlayFormulaPart.Right || editing?.Working?.Last?.Kind == OverlayFormulaTokenKind.Compare)
      return L(
        name + "은(는) 글자라서 크기를 비교할 수 없어요. 숫자인 값을 골라 주세요.",
        name + " is words, so it can't be compared. Pick a value that is a number."
      );
    return L(
      name + "은(는) 글자라서 계산에 쓸 수 없어요. 글 안에 넣어 주세요.",
      name + " is words, so it can't be calculated. Put it in the text instead."
    );
  }
}
