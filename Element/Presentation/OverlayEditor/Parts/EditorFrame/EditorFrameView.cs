using Element.Presentation.Ui.Declarative.Controls;
using ElementUI;
using static Element.Presentation.OverlayEditor.Parts.EditorControls;
using static Element.Presentation.Ui.Theme.UiLocale;
using static ElementUI.UI;
using Font = ElementUI.Font;

namespace Element.Presentation.OverlayEditor.Parts.EditorFrame;

/// <summary>
/// An editor's header along the top: what is edited on the left (with a
/// notice after it when there is no dock to show it), Cancel and Done on the
/// right, its ends in line with the content below.
/// </summary>
internal sealed class EditorFrameView : View<EditorFrameViewModel>
{
  private static readonly UiColor Slate = new UnityEngine.Color(.2f, .216f, .24f);

  public EditorFrameView(EditorFrameViewModel vm)
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
        Done().Hint(L("⌘/Ctrl+Enter나 Esc로도 완료돼요", "⌘/Ctrl+Enter or Esc also finishes"))
      )
      .Frame(height: EditorFramePanel.TopBar);

  // An editor with an input dock keeps the highlight color for what is picked
  // in it: its Done is a slate lighter than Cancel. Elsewhere Done is the tinted main action.
  private View Done() =>
    VM.InDock
      ? Button(
          UI.Text(L("완료", "Done"))
            .LineLimit(1)
            .Frame(maxWidth: float.PositiveInfinity, maxHeight: float.PositiveInfinity),
          () => VM.Done,
          ButtonStyle.Plain
        )
        .ButtonStyle(new ButtonSurfaceStyle(Slate, 8))
        .Named(VM.Name + " done")
        .Frame(width: 96, height: 36)
      : WideButton(L("완료", "Done"), VM.Name + " done", () => VM.Done, 96, 36, ButtonStyle.Prominent);
}
