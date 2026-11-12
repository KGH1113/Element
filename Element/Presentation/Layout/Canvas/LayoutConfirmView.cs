using ElementUI;
using static Element.Presentation.Layout.Canvas.LayoutParts;
using static Element.Presentation.Ui.Theme.UiLocale;
using static ElementUI.UI;

namespace Element.Presentation.Layout.Canvas;

/// <summary>The question on the dialog: its title, what the change does, then Cancel and the change on the right.</summary>
internal sealed class LayoutConfirmView : View<LayoutConfirmModel>
{
  public LayoutConfirmView(LayoutConfirmModel vm)
    : base(vm) { }

  public override View Body =>
    VStack(
        spacing: 16,
        Text(VM.Title).Font(Font.Title.Sized(23)),
        Text(VM.Body).Font(Font.Callout).Foreground(Colors.Muted),
        HStack(
            spacing: 12,
            Spacer(),
            WideButton(L("취소", "Cancel"), "Cancel", () => VM.Cancel, 112, 40),
            WideButton(VM.ConfirmLabel, "Confirm", () => VM.Confirm, 116, 40, ButtonStyle.Prominent)
          )
          .Padding(new Insets(8, 0, 0, 0))
      )
      .Padding(new Insets(24, 28, 24, 28));
}
