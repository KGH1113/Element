using Element.Presentation.Ui.Declarative;
using Element.Presentation.Ui.Declarative.Controls;
using Element.Presentation.Ui.Graphics;
using ElementUI;
using static Element.Presentation.Ui.Theme.UiLocale;
using static ElementUI.UI;
using Font = ElementUI.Font;

namespace Element.Presentation.OverlayEditor.Parts.ValueSource;

/// <summary>
/// The menu of values a tool can read: each value in the text, the
/// one read now ticked, then "Another value" with where to find it.
/// </summary>
internal sealed class ValueSourceMenuView : View<ValueSourceMenuViewModel>
{
  public ValueSourceMenuView(ValueSourceMenuViewModel vm)
    : base(vm) { }

  public override View Body => VStack(spacing: 2, Rows()).Padding(6);

  // The values in the text (or a line saying there are none), then another value.
  private View[] Rows()
  {
    View values =
      VM.Items.Count == 0
        ? UI.Text(L("이 글에는 아직 값이 없어요", "This text has no value yet"))
          .Font(Font.Caption.Sized(12))
          .Foreground(Colors.Muted)
          .LineLimit(1)
          .Padding(new Insets(0, 10, 0, 10))
          .Frame(maxWidth: float.PositiveInfinity, height: 28, alignment: Alignment.Leading)
        : VStack(spacing: 2, ForEach(VM.Items, item => item.Key, slot => new ValueSourceItemView(slot)));
    return new[]
    {
      values,
      Divider().Padding(new Insets(4, 8, 4, 8)),
      Button(
          VStack(
              spacing: 1,
              alignment: HAlignment.Leading,
              UI.Text(L("다른 값 고르기", "Another value")).Font(Font.Caption.Sized(13)).LineLimit(1),
              UI.Text(L("아래 값 목록에서 눌러요", "Click it in the values below"))
                .Font(Font.Caption.Sized(11))
                .Foreground(Colors.Muted)
                .LineLimit(1)
            )
            .Padding(new Insets(0, 32, 0, 10))
            .Frame(maxWidth: float.PositiveInfinity, height: 44, alignment: Alignment.Leading),
          () => VM.Other,
          ButtonStyle.Row
        )
        .Named("Follow another value"),
    };
  }
}
