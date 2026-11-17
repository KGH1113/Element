using Element.Presentation.Ui.Declarative;
using Element.Presentation.Ui.Declarative.Controls;
using ElementUI;
using static ElementUI.UI;

namespace Element.Presentation.OverlayEditor.Parts.Inspector;

/// <summary>
/// An editor's property column: a heading over a row per property, each
/// naming what it is and showing what it holds; or, while a row's tool is
/// open below, one back row naming that tool.
/// </summary>
internal sealed class InspectorView : View<InspectorViewModel>
{
  public const float RowHeight = InspectorRowStyle.Height,
    BackRowHeight = InspectorRowStyle.BackHeight,
    RowGap = InspectorRowStyle.Gap,
    HeadingHeight = InspectorRowStyle.HeadingHeight;

  private readonly float _width;

  public InspectorView(InspectorViewModel vm, float width)
    : base(vm) => _width = width;

  public override View Body =>
    If(
        () => VM.ToolOpen.Value,
        () => InspectorRowStyle.Back(() => VM.Open.Value, () => VM.Back, "Inspector back"),
        () =>
          VStack(
            spacing: RowGap,
            InspectorRowStyle.Heading(VM.Heading),
            ForEach(VM.Rows, row => row.Name, slot => new InspectorRowView(slot))
          )
      )
      .Frame(width: _width, alignment: Alignment.TopLeading);
}
