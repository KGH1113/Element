using Element.Presentation.Ui.Declarative.Controls;
using ElementUI;
using static Element.Presentation.Layout.Canvas.LayoutParts;
using static Element.Presentation.Ui.Theme.UiLocale;
using static ElementUI.UI;
using Font = ElementUI.Font;

namespace Element.Presentation.Layout.ContentEditors.Shell;

/// <summary>An editor's top line: the shortcuts or a notice on the left, Cancel and Done on the right.</summary>
internal sealed class OverlayEditorFrameView : View<OverlayEditorFrameModel>
{
  public OverlayEditorFrameView(OverlayEditorFrameModel vm)
    : base(vm) { }

  public override View Body =>
    HStack(
      spacing: 8,
      UI.Text(() => VM.Line.Value)
        .Font(Font.Caption.Sized(13))
        .Foreground(() => VM.Ink.Value)
        .LineLimit(1)
        .Named("Shortcuts")
        .Frame(maxWidth: float.PositiveInfinity, alignment: Alignment.Leading)
        .Padding(new Insets(0, 0, 0, 10)),
      WideButton(L("취소", "Cancel"), VM.Name + " cancel", () => VM.Cancel, 100, 38),
      WideButton(L("완료", "Done"), VM.Name + " done", () => VM.Done, 104, 38, ButtonStyle.Prominent)
    );
}
