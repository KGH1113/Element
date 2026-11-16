using Element.Presentation.Layout.ContentEditors.Shell;
using ElementUI;
using static ElementUI.UI;

namespace Element.Presentation.Layout.ContentEditors.Text;

/// <summary>
/// The Animate tool in the text editor's property column: its two switches
/// as property rows, and under them, while the color follows a value, that
/// color's controls.
/// </summary>
internal sealed class TextAnimateView : View<TextAnimateModel>
{
  public const float ColorGap = 14;

  private readonly float _width;

  public TextAnimateView(TextAnimateModel vm, float width)
    : base(vm) => _width = width;

  public override View Body =>
    VStack(
        spacing: 0,
        VStack(
          spacing: OverlayInspectorView.RowGap,
          ForEach(VM.Rows, row => row.Name, slot => new OverlayInspectorRowView(slot))
        ),
        If(
          () => VM.ColorShown.Value,
          () => new OverlayValueColorView(VM.ValueColor).Padding(new Insets(ColorGap, 0, 0, 0))
        )
      )
      .Frame(width: _width, alignment: Alignment.TopLeading);
}
