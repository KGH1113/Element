using ElementUI;
using static Element.Presentation.OverlayEditor.Parts.EditorControls;
using static Element.Presentation.Ui.Theme.UiLocale;
using static ElementUI.UI;

namespace Element.Presentation.OverlayEditor.Parts.Confirm;

/// <summary>The question on the dialog: its title, what the change does, then Cancel and the change on the right.</summary>
internal sealed class ConfirmView : View<ConfirmViewModel>
{
  public ConfirmView(ConfirmViewModel vm)
    : base(vm) { }

  public override View Body => VStack(spacing: 16, Parts()).Padding(new Insets(24, 28, 24, 28));

  // The title, what the change does when it says anything, then the two buttons.
  private View[] Parts()
  {
    View buttons = HStack(
        spacing: 12,
        Spacer(),
        WideButton(VM.CancelLabel, "Cancel", () => VM.Cancel, 128, 40),
        WideButton(VM.ConfirmLabel, "Confirm", () => VM.Confirm, 116, 40, ButtonStyle.Prominent)
      )
      .Padding(new Insets(8, 0, 0, 0));
    View title = Text(VM.Title).Font(Font.Title.Sized(23));
    return VM.Body.Length > 0
      ? new[] { title, Text(VM.Body).Font(Font.Callout).Foreground(Colors.Muted), buttons }
      : new[] { title, buttons };
  }
}
