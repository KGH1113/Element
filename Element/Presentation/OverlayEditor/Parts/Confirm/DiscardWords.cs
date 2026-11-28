using static Element.Presentation.Ui.Theme.UiLocale;

namespace Element.Presentation.OverlayEditor.Parts.Confirm;

/// <summary>
/// The question every editor's Cancel asks when something changed, in the
/// same words wherever it is asked: the layout editor and the full-screen editors.
/// </summary>
internal static class DiscardWords
{
  public static string Title => L("바뀐 내용을 버릴까요?", "Discard your changes?");

  /// <summary>Throws the edit away.</summary>
  public static string Discard => L("버리기", "Discard");

  /// <summary>Leaves things as they are; focused, so Enter is safe.</summary>
  public static string KeepEditing => L("계속 편집", "Keep editing");
}
