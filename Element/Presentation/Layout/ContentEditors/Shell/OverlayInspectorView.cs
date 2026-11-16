using System.Collections.Generic;
using Element.Presentation.Ui.Declarative;
using Element.Presentation.Ui.Declarative.Controls;
using ElementUI;
using static ElementUI.UI;

namespace Element.Presentation.Layout.ContentEditors.Shell;

/// <summary>
/// An editor's property column: a heading over a row per property, each
/// naming what it is and showing what it holds; or, while a row's tool is
/// open below, one back row naming that tool.
/// </summary>
internal sealed class OverlayInspectorView : View<OverlayInspectorModel>
{
  public const float RowHeight = OverlayInspectorRow.Height,
    BackRowHeight = OverlayInspectorRow.BackHeight,
    RowGap = OverlayInspectorRow.Gap,
    HeadingHeight = OverlayInspectorRow.HeadingHeight;

  private readonly float _width;

  public OverlayInspectorView(OverlayInspectorModel vm, float width)
    : base(vm) => _width = width;

  public override View Body =>
    If(
        () => VM.ToolOpen.Value,
        () => OverlayInspectorRow.Back(() => VM.Open.Value, () => VM.Back, "Inspector back"),
        () =>
          VStack(
            spacing: RowGap,
            OverlayInspectorRow.Heading(VM.Heading),
            ForEach(VM.Rows, row => row.Name, slot => new OverlayInspectorRowView(slot))
          )
      )
      .Frame(width: _width, alignment: Alignment.TopLeading);
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
    Button(Content(), () => VM.Click, ButtonStyle.Plain)
      .Background(() => VM.Fill.Value, OverlayInspectorRow.Radius)
      .Named(VM.Name);

  private View Content()
  {
    List<View> trailing = new();
    if (VM.ShowsValue)
      trailing.Add(OverlayInspectorRow.Value(() => VM.ValueText.Value, () => VM.ValueInk.Value));
    if (VM.Unit != null)
      trailing.Add(OverlayInspectorRow.Unit(VM.Unit));
    if (VM.Toggle)
      trailing.Add(new SwitchIndicator(() => VM.SwitchOn.Value, () => VM.Available.Value));
    if (VM.Opens)
      trailing.Add(OverlayInspectorRow.Chevron());
    return OverlayInspectorRow.Of(
      Lead(),
      OverlayInspectorRow.Name(VM.Label, () => VM.Ink.Value),
      VM.Toggle,
      trailing.ToArray()
    );
  }

  private View Lead()
  {
    if (VM.Swatch)
      return new SwatchDot(() => VM.SwatchColor.Value, () => VM.SwatchGradient.Value);
    if (VM.Icon is { } icon)
      return OverlayInspectorRow.Icon(icon, () => VM.Ink.Value);
    return VM.Glyph != null ? OverlayInspectorRow.Glyph(VM.Glyph) : null;
  }
}
