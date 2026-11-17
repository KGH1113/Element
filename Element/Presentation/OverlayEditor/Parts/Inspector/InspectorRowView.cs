using Element.Presentation.OverlayEditor.Parts.Property;
using Element.Presentation.Ui.Declarative;
using Element.Presentation.Ui.Declarative.Controls;
using ElementUI;
using static ElementUI.UI;

namespace Element.Presentation.OverlayEditor.Parts.Inspector;

/// <summary>
/// One property's row: what leads it (a swatch, an icon or letters), its
/// name, then on the right what it holds with its unit, its switch, or a
/// chevron for a row that opens a tool. The whole row is the button and
/// lights up while its tool is open or a dragged value is over it.
/// </summary>
internal sealed class InspectorRowView : View<PropertyButtonViewModel>
{
  public InspectorRowView(ItemSlot<PropertyButtonViewModel> slot)
    : base(slot) { }

  public override View Body =>
    Button(Content(), () => VM.Click, ButtonStyle.Plain)
      .ButtonStyle(new ButtonSurfaceStyle(() => VM.Fill.Value, InspectorRowStyle.Radius))
      .Named(VM.Name);

  private View Content() =>
    InspectorRowStyle.Of(
      Lead(),
      InspectorRowStyle.Name(VM.Label, () => VM.Ink.Value),
      VM.Toggle,
      VM.ShowsValue ? InspectorRowStyle.Value(() => VM.ValueText.Value, () => VM.ValueInk.Value) : null,
      VM.Unit != null ? InspectorRowStyle.Unit(VM.Unit) : null,
      VM.Toggle ? new SwitchIndicator(() => VM.SwitchOn.Value, () => VM.Available.Value) : null,
      VM.Opens ? InspectorRowStyle.Chevron() : null
    );

  private View Lead()
  {
    if (VM.Swatch)
      return new SwatchDot(() => VM.SwatchColor.Value, () => VM.SwatchGradient.Value);
    if (VM.Icon is { } icon)
      return InspectorRowStyle.Icon(icon, () => VM.Ink.Value);
    return VM.Glyph != null ? InspectorRowStyle.Glyph(VM.Glyph) : null;
  }
}
