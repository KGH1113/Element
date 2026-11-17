using Element.Presentation.Ui.Declarative;
using Element.Presentation.Ui.Declarative.Controls;
using Element.Presentation.Ui.Theme;
using ElementUI;
using static ElementUI.UI;

namespace Element.Presentation.OverlayEditor.Canvas.AddMenu;

/// <summary>One row of an add menu list.</summary>
internal sealed class AddMenuItemView : View<AddMenuItemViewModel>
{
  private readonly bool _large;

  public AddMenuItemView(ItemSlot<AddMenuItemViewModel> slot, bool large)
    : base(slot) => _large = large;

  public override View Body =>
    Button(
        HStack(
            spacing: _large ? 14 : 12,
            Image(ElementIcons.Of(VM.Row.Icon), _large ? 28 : 22)
              .Foreground(VM.Row.Enabled ? UiPalette.SoftAccent : Colors.Muted),
            Words()
          )
          // The whole row is the button, its words lined up on the left.
          .Frame(maxWidth: float.PositiveInfinity, maxHeight: float.PositiveInfinity, alignment: Alignment.Leading),
        () => VM.Add,
        ButtonStyle.Row
      )
      .Named(VM.Row.Name)
      .Frame(
        height: _large ? 64
        : VM.Row.Detail != null ? 52
        : 44
      );

  // Its name, and under it what it adds or why it can't be added now.
  private View Words()
  {
    View title = Text(VM.Row.Title)
      .Font(_large ? Font.Body : Font.Callout)
      .Foreground(VM.Row.Enabled ? Colors.Foreground : Colors.Muted)
      .LineLimit(1);
    if (VM.Row.Detail == null)
      return title;
    return VStack(
      spacing: 2,
      title,
      Text(VM.Row.Detail)
        .Font(Font.Caption.Sized(12))
        .Foreground(VM.Row.Enabled ? Colors.Muted : Colors.Danger)
        .LineLimit(1)
    );
  }
}
