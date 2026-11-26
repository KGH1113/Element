using Element.Domain.Overlay.Values;
using Element.Domain.Overlay.Variables;
using ElementUI;
using static Element.Presentation.Ui.Theme.UiLocale;

namespace Element.Presentation.OverlayEditor.Parts.Formula;

/// <summary>Why something can't go where it was put, or why a field can't be finished, in words the player can act on.</summary>
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
      _ => L("아무거나", "anything"),
    };

  /// <summary>A condition's slot as the dock names it.</summary>
  public static string SlotName(int slot) =>
    slot switch
    {
      OverlayPiece.Left => L("비교할 값", "Compared"),
      OverlayPiece.Compare => L("연산자", "Operator"),
      OverlayPiece.Right => L("비교 당할 값", "Compared with"),
      OverlayPiece.Result => L("결과값", "Result"),
      OverlayPiece.Default => L("기본값", "Default"),
      _ => L("묶음", "Group"),
    };

  /// <summary>Why <paramref name="value"/> can't go where the dock puts values now.</summary>
  public static string Why(OverlayValueDefinition value, FormulaEditing editing)
  {
    string name = ValueSectionNames.ValueName(value);
    OverlayValueType found = OverlayVariableTypes.TypeOf(value);
    OverlayValueType needed = editing?.Active == true ? editing.Kind : OverlayValueType.Number;
    if (needed == OverlayValueType.Compare)
      return L("연산자 칸에는 아래 비교 기호만 들어가요.", "The operator takes one of the comparisons below.");
    if (editing?.Active == true && needed == OverlayValueType.Any)
      needed = OverlayValueType.Number;
    return L(
      name
        + "은(는) "
        + TypeName(found)
        + "라서 여기에는 넣을 수 없어요. 이 칸에는 "
        + TypeName(needed)
        + "가 들어가요.",
      name + " is " + TypeName(found) + ", so it can't go here. This takes " + TypeName(needed) + "."
    );
  }

  /// <summary>Why a field can't be finished as it is, and what to do.</summary>
  [Pure]
  public static string Explain(OverlayTypeProblem problem)
  {
    if (problem == null)
      return null;
    string where =
      problem.Slot >= 0
        ? L("조건의 " + SlotName(problem.Slot) + ": ", "In the condition's " + SlotName(problem.Slot) + ": ")
        : "";
    string needed = TypeName(problem.Needed),
      found = TypeName(problem.Found);
    return where
      + problem.Kind switch
      {
        OverlayTypeProblemKind.Empty => L("비어 있어요. 값을 넣어 주세요.", "it's empty. Put a value in."),
        OverlayTypeProblemKind.WrongType => L(
          needed + "가 들어가야 하는데 " + found + "예요.",
          "it needs " + needed + " but has " + found + "."
        ),
        OverlayTypeProblemKind.SideBySide => L(
          "값이 나란히 있어요. 숫자는 + − × ÷ 로 이어 주세요.",
          "two values sit side by side. Join numbers with + − × ÷."
        ),
        OverlayTypeProblemKind.NotNumber => L(
          found + "는 계산할 수 없어요. 숫자만 + − × ÷ 로 이을 수 있어요.",
          found + " can't be calculated. Only numbers join with + − × ÷."
        ),
        OverlayTypeProblemKind.Dangling => L("연산 기호 뒤에 숫자가 없어요.", "an operator has no number after it."),
        _ => L("조건과 묶음이 너무 깊이 겹쳐 있어요.", "conditions and groups sit too deep inside each other."),
      };
  }
}
