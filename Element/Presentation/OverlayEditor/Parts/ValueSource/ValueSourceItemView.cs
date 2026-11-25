using Element.Presentation.Ui.Declarative;
using Element.Presentation.Ui.Declarative.Controls;
using Element.Presentation.Ui.Graphics;
using ElementUI;
using static Element.Presentation.Ui.Theme.UiLocale;
using static ElementUI.UI;
using Font = ElementUI.Font;

namespace Element.Presentation.OverlayEditor.Parts.ValueSource;

/// <summary>One value in the menu, ticked while it is the one compared.</summary>
internal sealed class ValueSourceItemView : View<ValueSourceItemViewModel>
{
  public ValueSourceItemView(ItemSlot<ValueSourceItemViewModel> slot)
    : base(slot) { }

  public override View Body =>
    Button(
        HStack(
            spacing: 0,
            Image(ElementIcons.Of(UiIconKind.Check), 14)
              .Foreground(Colors.Accent)
              .Hidden(!VM.Followed)
              .Frame(width: 22, alignment: Alignment.Leading),
            UI.Text(VM.Name).Font(Font.Caption.Sized(13)).LineLimit(1),
            Spacer()
          )
          .Padding(new Insets(0, 10, 0, 10))
          .Frame(maxWidth: float.PositiveInfinity, height: RowHeight),
        () => VM.Pick,
        ButtonStyle.Row
      )
      .Named("Follow " + VM.Key);

  private const float RowHeight = 34;
}
