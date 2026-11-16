using System.Collections.Generic;
using Element.Presentation.Ui.Declarative;
using Element.Presentation.Ui.Declarative.Controls;
using Element.Presentation.Ui.Graphics;
using Element.Presentation.Ui.Theme;
using ElementUI;
using static ElementUI.UI;
using Font = ElementUI.Font;

namespace Element.Presentation.Layout.ContentEditors.Shell;

/// <summary>
/// An editor's property column: a heading over a row per property, each
/// naming what it is and showing what it holds; or, while a row's tool is
/// open below, one back row naming that tool.
/// </summary>
internal sealed class OverlayInspectorView : View<OverlayInspectorModel>
{
  public const float RowHeight = 40,
    BackRowHeight = 36,
    RowGap = 6,
    HeadingHeight = 22;

  private readonly float _width;

  public OverlayInspectorView(OverlayInspectorModel vm, float width)
    : base(vm) => _width = width;

  public override View Body =>
    If(
        () => VM.ToolOpen.Value,
        BackRow,
        () =>
          VStack(
            spacing: RowGap,
            Section(VM.Heading)
              .Frame(height: HeadingHeight, alignment: Alignment.BottomLeading)
              .Padding(new Insets(0, 4, 0, 0)),
            ForEach(VM.Rows, row => row.Name, slot => new OverlayInspectorRowView(slot))
          )
      )
      .Frame(width: _width, alignment: Alignment.TopLeading);

  // Back to the rows, naming the tool open below it. It reads as the dimmed
  // view at rest but is opaque, so its hover doesn't let the overlay through.
  private View BackRow() =>
    Button(
        HStack(
            spacing: 6,
            Image(ElementIcons.Of(UiIconKind.Back), 16).Foreground(Colors.Muted),
            UI.Text(() => VM.Open.Value).Font(Font.Caption.Sized(13)).LineLimit(1),
            Spacer()
          )
          .Padding(0, 8)
          .Frame(height: BackRowHeight),
        () => VM.Back,
        ButtonStyle.Plain
      )
      .Background(UiPalette.EditorDim, BackRowHeight / 2)
      .Named("Inspector back");
}

/// <summary>
/// One property's row: what leads it (a swatch, an icon or letters), its
/// name, then on the right what it holds with its unit, its switch, or a
/// chevron for a row that opens a tool. The whole row is the button and
/// lights up while its tool is open or a dragged value is over it.
/// </summary>
internal sealed class OverlayInspectorRowView : View<OverlayBarButtonModel>
{
  public OverlayInspectorRowView(ItemSlot<OverlayBarButtonModel> slot)
    : base(slot) { }

  public override View Body =>
    Button(Content(), () => VM.Click, ButtonStyle.Plain).Background(() => VM.Fill.Value, 10).Named(VM.Name);

  private View Content()
  {
    List<View> parts = new() { Lead().Frame(width: 22, alignment: Alignment.Leading) };
    parts.Add(
      UI.Text(VM.Label)
        .Font(Font.Caption.Sized(13))
        .Foreground(() => VM.Ink.Value)
        .LineLimit(1)
        .Padding(new Insets(0, 8, 0, 0))
    );
    parts.Add(Spacer(minLength: 8));
    if (VM.ShowsValue)
      parts.Add(
        UI.Text(() => VM.ValueText.Value).Font(Font.Caption.Sized(13)).Foreground(() => VM.ValueInk.Value).LineLimit(1)
      );
    if (VM.Unit != null)
      parts.Add(
        UI.Text(VM.Unit)
          .Font(Font.Caption.Sized(12))
          .Foreground(Colors.Muted)
          .LineLimit(1)
          .Padding(new Insets(0, 3, 0, 0))
      );
    if (VM.Toggle)
      parts.Add(new SwitchIndicator(() => VM.SwitchOn.Value, () => VM.Available.Value));
    if (VM.Opens)
      parts.Add(
        Image(ElementIcons.Of(UiIconKind.Forward), 14).Foreground(Colors.Muted).Padding(new Insets(0, 6, 0, 0))
      );
    return HStack(spacing: 0, parts.ToArray())
      .Padding(new Insets(0, 12, 0, VM.Toggle ? 10 : 12))
      .Frame(maxWidth: float.PositiveInfinity, height: OverlayInspectorView.RowHeight);
  }

  private View Lead()
  {
    if (VM.Swatch)
      return new SwatchDot(() => VM.SwatchColor.Value, () => VM.SwatchGradient.Value);
    if (VM.Icon is { } icon)
      return Image(ElementIcons.Of(icon), 18).Foreground(() => VM.Ink.Value);
    if (VM.Glyph != null)
      return UI.Text(VM.Glyph).Font(Font.Callout).Foreground(Colors.Muted).LineLimit(1);
    return Spacer().Frame(width: 18, height: 18);
  }
}
