using Element.Presentation.Ui.Declarative;
using Element.Presentation.Ui.Declarative.Controls;
using ElementUI;
using static ElementUI.UI;
using Font = ElementUI.Font;

namespace Element.Presentation.OverlayEditor.Parts.Property;

/// <summary>
/// One button of an editor's bar: a capsule a little lighter than the bar,
/// with what leads its name (a swatch, an icon or letters), the name, what it
/// holds with its unit, and a switch. The whole capsule lights up on hover.
/// </summary>
internal sealed class PropertyButtonView : View<PropertyButtonViewModel>
{
  public const float Height = 32;

  public PropertyButtonView(ItemSlot<PropertyButtonViewModel> slot)
    : base(slot) { }

  public override View Body =>
    Button(Content(), () => VM.Click, ButtonStyle.Plain)
      .ButtonStyle(new ButtonSurfaceStyle(() => VM.Fill.Value, Height / 2))
      .Named(VM.Name)
      .Frame(height: Height);

  // Lead, name, value and unit, switch: each part only when the button has it.
  private View Content() =>
    HStack(
        spacing: 0,
        Lead()?.Padding(new Insets(0, 0, 0, 7)),
        UI.Text(VM.Label).Font(Font.Caption).Foreground(() => VM.Ink.Value).LineLimit(1),
        VM.ShowsValue
          ? UI.Text(() => VM.ValueText.Value)
            .Font(Font.Caption)
            .Foreground(() => VM.ValueInk.Value)
            .LineLimit(1)
            .Padding(new Insets(0, 6, 0, 0))
          : null,
        VM.Unit != null
          ? UI.Text(VM.Unit)
            .Font(Font.Caption.Sized(12))
            .Foreground(Colors.Muted)
            .LineLimit(1)
            .Padding(new Insets(0, 2, 0, 0))
          : null,
        VM.Toggle
          ? new SwitchIndicator(() => VM.SwitchOn.Value, () => VM.Available.Value).Padding(new Insets(0, 8, 0, 0))
          : null
      )
      // A switch sits a little closer to the end, like the settings' switches.
      .Padding(new Insets(0, 12, 0, VM.Toggle ? 10 : 12))
      .Frame(height: Height);

  private View Lead()
  {
    if (VM.Swatch)
      return new SwatchDot(() => VM.SwatchColor.Value, () => VM.SwatchGradient.Value);
    if (VM.Icon is { } icon)
      return Image(ElementIcons.Of(icon), 18).Foreground(() => VM.Ink.Value);
    if (VM.Glyph != null)
      // Letters take the room they need, at least an icon's.
      return UI.Text(VM.Glyph).Font(Font.Callout).Foreground(Colors.Muted).LineLimit(1).Frame(minWidth: 18);
    return null;
  }
}
