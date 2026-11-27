using Element.Presentation.Ui.Declarative.Controls;
using ElementUI;
using static Element.Presentation.Ui.Theme.UiLocale;
using static ElementUI.UI;
using Font = ElementUI.Font;

namespace Element.Presentation.OverlayEditor.Parts.Grow;

/// <summary>
/// Grow from's popover: what it picks, the nine points, and beside them
/// Auto and a line saying what the choice does.
/// </summary>
internal sealed class GrowPopoverView : View<GrowPickerViewModel>
{
  private readonly Insets _insets = new(12, 14, 14, 14);

  public GrowPopoverView(GrowPickerViewModel vm)
    : base(vm) { }

  public override View Body =>
    VStack(
        spacing: 8,
        UI.Text(L("크기가 바뀔 때 고정할 점", "Point that stays when the size changes"))
          .Font(Font.Caption.Sized(13))
          .LineLimit(1)
          .Frame(height: 22, alignment: Alignment.Leading),
        HStack(
          spacing: 14,
          alignment: VAlignment.Top,
          new GrowPointsView(VM),
          VStack(
              spacing: 6,
              Button(
                  UI.Text(L("자동", "Auto"))
                    .Font(Font.Caption.Sized(12))
                    .Foreground(() => VM.AutoInk.Value)
                    .LineLimit(1)
                    .Padding(0, 13)
                    .Frame(height: 26),
                  () => VM.Auto,
                  ButtonStyle.Plain
                )
                .Background(() => VM.AutoFill.Value, 13)
                .Named("Grow automatically"),
              UI.Text(() => VM.Caption.Value).Font(Font.Caption.Sized(12)).Foreground(Colors.Muted).Named("Caption")
            )
            .Frame(maxWidth: float.PositiveInfinity, alignment: Alignment.TopLeading)
        )
      )
      .Padding(_insets);
}
