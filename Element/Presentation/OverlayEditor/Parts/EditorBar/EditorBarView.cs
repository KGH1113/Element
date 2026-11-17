using Element.Presentation.OverlayEditor.Parts.Property;
using ElementUI;
using static ElementUI.UI;

namespace Element.Presentation.OverlayEditor.Parts.EditorBar;

/// <summary>An editor's bar: its buttons side by side, in the order given.</summary>
internal sealed class EditorBarView : View<EditorBarViewModel>
{
  public EditorBarView(EditorBarViewModel vm)
    : base(vm) { }

  public override View Body =>
    HStack(spacing: 4, ForEach(VM.Buttons, button => button.Name, slot => new PropertyButtonView(slot)))
      .Padding((EditorBarPanel.Height - PropertyButtonView.Height) * .5f);
}
