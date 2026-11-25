using Element.Presentation.OverlayEditor.Parts.Inspector;
using ElementUI;
using static ElementUI.UI;

namespace Element.Presentation.OverlayEditor.TextEditor;

/// <summary>
/// The Animate tool in the text editor's property column: its switch as a
/// property row.
/// </summary>
internal sealed class TextAnimateView : View<TextAnimateViewModel>
{
  private readonly float _width;

  public TextAnimateView(TextAnimateViewModel vm, float width)
    : base(vm) => _width = width;

  public override View Body =>
    VStack(spacing: InspectorRowStyle.Gap, ForEach(VM.Rows, row => row.Name, slot => new InspectorRowView(slot)))
      .Frame(width: _width, alignment: Alignment.TopLeading);
}
