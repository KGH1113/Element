using Element.Domain.Overlay.Variables;
using static Element.Presentation.Ui.Theme.UiLocale;

namespace Element.Presentation.OverlayEditor.Parts.Formula;

/// <summary>The words a variable reads with in the editors, in the language shown.</summary>
internal static class FormulaWords
{
  /// <summary>"만약 … 이면 … 아니면", true and false, in the language shown now.</summary>
  public static OverlayVariableWords Current =>
    new(
      L("만약", "if"),
      L("이면", "then"),
      L("아니면", "otherwise"),
      L("참", "true"),
      L("거짓", "false"),
      L("없는 값", "missing"),
      L("이름 없는 값", "Unnamed value")
    );
}
