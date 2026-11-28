using Element.Presentation.Ui.Theme;
using TMPro;
using UnityEngine;

namespace Element.Presentation.Ui.Widgets;

/// <summary>
/// The caret of a field typed into in the editors: in the highlight color
/// and two units wide, so where the typing goes is plain on the dark, with
/// the selection a wash of the same color.
/// </summary>
internal static class UiCaret
{
  public static void Show(TMP_InputField field)
  {
    if (field == null)
      return;
    Color accent = UiPalette.SoftAccent;
    field.customCaretColor = true;
    field.caretColor = accent;
    field.caretWidth = 2;
    field.caretBlinkRate = .9f;
    accent.a = .35f;
    field.selectionColor = accent;
  }
}
