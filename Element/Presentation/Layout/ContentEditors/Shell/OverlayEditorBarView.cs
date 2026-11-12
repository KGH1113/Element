using ElementUI;
using static ElementUI.UI;

namespace Element.Presentation.Layout.ContentEditors.Shell;

/// <summary>An editor's bar: its buttons side by side, in the order given.</summary>
internal sealed class OverlayEditorBarView : View<OverlayEditorBarModel>
{
  public OverlayEditorBarView(OverlayEditorBarModel vm)
    : base(vm) { }

  public override View Body =>
    HStack(spacing: 4, ForEach(VM.Buttons, button => button.Name, slot => new OverlayBarButtonView(slot)))
      .Padding((OverlayEditorBar.Height - OverlayBarButtonView.Height) * .5f);
}
