using Element.Presentation.Ui.Declarative.Controls;
using ElementUI;
using static Element.Presentation.Layout.Canvas.LayoutParts;
using static Element.Presentation.Ui.Theme.UiLocale;
using static ElementUI.UI;
using Font = ElementUI.Font;

namespace Element.Presentation.Layout.ContentEditors.Shell;

/// <summary>
/// An editor's header along the top: what is edited on the left (with a
/// notice after it when there is no dock to show it), Cancel and Done on the
/// right, its ends in line with the content below.
/// </summary>
internal sealed class OverlayEditorFrameView : View<OverlayEditorFrameModel>
{
  public OverlayEditorFrameView(OverlayEditorFrameModel vm)
    : base(vm) { }

  public override View Body =>
    HStack(
        spacing: 8,
        UI.Text(VM.Title).Font(Font.Callout.Bolded()).LineLimit(1).Named("Editor title"),
        UI.Text(() => VM.HeaderNotice.Value)
          .Font(Font.Caption.Sized(13))
          .Foreground(() => VM.Ink.Value)
          .LineLimit(1)
          .Named("Header notice")
          .Padding(new Insets(0, 12, 0, 0))
          .Frame(maxWidth: float.PositiveInfinity, alignment: Alignment.Leading),
        WideButton(L("취소", "Cancel"), VM.Name + " cancel", () => VM.Cancel, 92, 36),
        WideButton(L("완료", "Done"), VM.Name + " done", () => VM.Done, 96, 36, ButtonStyle.Prominent)
          .Hint(L("⌘/Ctrl+Enter나 Esc로도 완료돼요", "⌘/Ctrl+Enter or Esc also finishes"))
      )
      .Frame(height: OverlayEditorFrame.TopBar);
}
