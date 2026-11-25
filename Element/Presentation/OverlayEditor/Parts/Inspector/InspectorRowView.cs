using Element.Domain.Overlay.Values;
using Element.Presentation.OverlayEditor.Parts.Field;
using Element.Presentation.OverlayEditor.Parts.Property;
using Element.Presentation.Ui.Theme;
using Element.Presentation.Ui.Declarative;
using Element.Presentation.Ui.Declarative.Controls;
using ElementUI;
using static ElementUI.UI;
using Font = ElementUI.Font;

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

  public override View Body
  {
    get
    {
      View row = Button(Content(), () => VM.Click, ButtonStyle.Plain)
        .ButtonStyle(new ButtonSurfaceStyle(() => VM.Fill.Value, InspectorRowStyle.Radius))
        .Named(VM.Name);
      // A row that starts a group of the column carries the group's heading.
      return VStack(
        spacing: InspectorRowStyle.Gap,
        If(() => VM.Section.Value.Length > 0, () => InspectorRowStyle.Heading(() => VM.Section.Value)),
        row
      );
    }
  }

  private View Content() => VM.IsField ? FieldContent() : ButtonContent();

  // A field (칸): its type's mark, its name, then what it holds as chips and what that works out to.
  private View FieldContent() =>
    InspectorRowStyle.Of(
      VM.Field is { } type ? TypeMarks.Mark(type, 20) : FieldGlyph(VM.FieldGlyph),
      InspectorRowStyle.Name(VM.Label, () => VM.Ink.Value),
      false,
      HStack(spacing: 4, ForEach(() => VM.Chips.Value, chip => chip.Identity, chip => new FieldChipView(chip))),
      Text(() => VM.Result.Value)
        .Font(Font.Caption.Sized(13))
        .Foreground(UiPalette.SoftAccent)
        .LineLimit(1)
        .Hidden(() => VM.Result.Value.Length == 0)
    );

  private static View FieldGlyph(string glyph) =>
    Text(glyph)
      .Font(Font.Caption.Sized(12))
      .Foreground(Colors.Muted)
      .LineLimit(1)
      .TextAlignment(TextAlign.Center)
      .Frame(width: 20, height: 20)
      .Background(new UnityEngine.Color(1, 1, 1, .1f), 6);

  private View ButtonContent() =>
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
