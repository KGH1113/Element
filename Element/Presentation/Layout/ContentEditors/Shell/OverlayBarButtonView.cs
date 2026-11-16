using System.Collections.Generic;
using Element.Presentation.Ui.Declarative;
using Element.Presentation.Ui.Declarative.Controls;
using ElementUI;
using static ElementUI.UI;
using Font = ElementUI.Font;

namespace Element.Presentation.Layout.ContentEditors.Shell;

/// <summary>
/// One button of an editor's bar: a capsule a little lighter than the bar,
/// with what leads its name (a swatch, an icon or letters), the name, what it
/// holds with its unit, and a switch. The whole capsule lights up on hover.
/// </summary>
internal sealed class OverlayBarButtonView : View<OverlayBarButtonModel>
{
  public const float Height = 32;

  public OverlayBarButtonView(ItemSlot<OverlayBarButtonModel> slot)
    : base(slot) { }

  public override View Body =>
    Button(Content(), () => VM.Click, ButtonStyle.Plain)
      .Background(() => VM.Fill.Value, Height / 2)
      .Named(VM.Name)
      .Frame(height: Height);

  // Lead, name, value and unit, switch: each part only when the button has it.
  private View Content()
  {
    List<View> parts = new();
    View lead = Lead();
    if (lead != null)
      parts.Add(lead.Padding(new Insets(0, 0, 0, 7)));
    parts.Add(UI.Text(VM.Label).Font(Font.Caption).Foreground(() => VM.Ink.Value).LineLimit(1));
    if (VM.ShowsValue)
      parts.Add(
        UI.Text(() => VM.ValueText.Value)
          .Font(Font.Caption)
          .Foreground(() => VM.ValueInk.Value)
          .LineLimit(1)
          .Padding(new Insets(0, 6, 0, 0))
      );
    if (VM.Unit != null)
      parts.Add(
        UI.Text(VM.Unit)
          .Font(Font.Caption.Sized(12))
          .Foreground(Colors.Muted)
          .LineLimit(1)
          .Padding(new Insets(0, 2, 0, 0))
      );
    if (VM.Toggle)
      parts.Add(new SwitchIndicator(() => VM.SwitchOn.Value, () => VM.Available.Value).Padding(new Insets(0, 8, 0, 0)));
    // A switch sits a little closer to the end, like the settings' switches.
    return HStack(spacing: 0, parts.ToArray()).Padding(new Insets(0, 12, 0, VM.Toggle ? 10 : 12)).Frame(height: Height);
  }

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
