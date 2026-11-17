using ElementUI;
using static Element.Presentation.OverlayEditor.Parts.EditorControls;
using static Element.Presentation.Ui.Theme.UiLocale;
using static ElementUI.UI;

namespace Element.Presentation.OverlayEditor.Kinds;

/// <summary>A key viewer's element toolbar: hand or foot, then Edit key viewer.</summary>
internal sealed class KeyViewerBarView : View<KeyViewerBarViewModel>
{
  public KeyViewerBarView(KeyViewerBarViewModel vm)
    : base(vm) { }

  public override View Body =>
    HStack(
        spacing: 8,
        Grip("Drag element toolbar"),
        Picker("", new[] { L("손", "Hand"), L("발", "Foot") }, Bind(() => VM.Viewer)).Frame(width: 172, height: 40),
        Separator(),
        WideButton(
            L("키뷰어 편집", "Edit key viewer"),
            "Edit key viewer",
            () => VM.Edit,
            172,
            40,
            ButtonStyle.Prominent
          )
          .Hint(
            L(
              "ElementNote에서 이 키뷰어의 탭을 열어요 · 더블클릭, Enter",
              "Opens this viewer's tab in ElementNote · Double-click, Enter"
            )
          )
      )
      .Padding(new Insets(8, 10, 8, 8));
}
