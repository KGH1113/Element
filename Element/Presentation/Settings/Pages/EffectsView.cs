using Element.Presentation.Ui.Declarative;
using Element.Presentation.Ui.Declarative.Controls;
using Element.Presentation.Ui.Graphics;
using ElementUI;
using TMPro;
using static Element.Presentation.Ui.Declarative.Controls.CardRows;
using static Element.Presentation.Ui.Declarative.Controls.SwitchCards;
using static Element.Presentation.Ui.Theme.UiLocale;
using static ElementUI.UI;

namespace Element.Presentation.Settings.Pages;

/// <summary>Effects: the removal switch, the preset cards in three, two or one columns, and when changes apply.</summary>
internal sealed class EffectsView : View<EffectsModel>
{
  internal const float CardHeight = 140;

  private readonly State<float> _width = new(1000);

  public EffectsView(EffectsModel vm)
    : base(vm) { }

  public override View Body =>
    ZStack(
      Alignment.TopLeading,
      new WidthReader(_width),
      VStack(
        spacing: 12,
        SwitchCard(
          L("이펙트 제거", "Effect removal"),
          () => VM.Status.Value,
          Bind(() => VM.Settings.Effects.Enabled),
          () => VM.HasPresets.Value
        ),
        HStack(
            Text(L("프리셋", "Presets")).Font(Font.Heading),
            Spacer(),
            Button(
                Text(L("프리셋 추가", "New preset")).Foreground(Colors.Accent).Frame(maxWidth: float.PositiveInfinity),
                () => VM.NewPreset,
                ButtonStyle.Prominent
              )
              .Frame(width: 156, height: 44)
          )
          .Padding(new Insets(4, 0, 4, 0)),
        If(
            () => VM.HasPresets.Value,
            () =>
              If(
                  () => _width.Value >= 600,
                  () => Gallery(3),
                  () =>
                    If(() => _width.Value >= 440, () => Gallery(2), () => Gallery(1))
                      .Frame(maxWidth: float.PositiveInfinity)
                )
                .Frame(maxWidth: float.PositiveInfinity),
            () =>
              Text(L("첫 프리셋을 만들어 보세요.", "Create your first preset."))
                .Font(Font.Heading)
                .Foreground(Colors.Muted)
                .Padding(new Insets(16, 0, 16, 0))
          )
          .Frame(maxWidth: float.PositiveInfinity, alignment: Alignment.TopLeading),
        Text(
            L(
              "변경 사항은 레벨을 시작하거나 재시작할 때 적용됩니다.",
              "Changes take effect when a level starts or restarts."
            )
          )
          .Font(Font.Caption)
          .Foreground(Colors.Muted)
      )
    );

  private View Gallery(int columns) => CardRows.Of(() => VM.Cards, card => card.Id, columns, 12, Card);

  /// <summary>A preset's card: click to apply; the pencil edits it; a bar on the left marks the selected one.</summary>
  private static View Card(ItemSlot<PresetCardModel[]> row, int column) =>
    ZStack(
      Alignment.TopLeading,
      Button(
          VStack(
              spacing: 6,
              Text(() => At(row.Value, column).Name)
                .Font(Font.Heading.Sized(20))
                .Foreground(Colors.Foreground)
                .Configure<TMP_Text>(text => text.overflowMode = TextOverflowModes.Ellipsis)
                .Frame(maxHeight: 54, alignment: Alignment.TopLeading)
                .Padding(new Insets(0, 0, 0, 36)),
              Spacer(),
              HStack(
                spacing: 12,
                Text(() => At(row.Value, column).Removes)
                  .Configure<TMP_Text>(text => text.overflowMode = TextOverflowModes.Ellipsis)
                  .Frame(maxWidth: float.PositiveInfinity, maxHeight: 20, alignment: Alignment.Leading),
                Text(() => At(row.Value, column).Shortcut)
              ),
              Text(() => At(row.Value, column).State.Value)
                .Foreground(() => At(row.Value, column).Selected.Value ? Colors.Accent : Colors.Muted)
            )
            .Font(Font.Caption)
            .Foreground(Colors.Muted)
            .Frame(
              maxWidth: float.PositiveInfinity,
              maxHeight: float.PositiveInfinity,
              alignment: Alignment.TopLeading
            ),
          () => At(row.Value, column).Apply,
          ButtonStyle.Card
        )
        .Frame(height: CardHeight),
      Spacer()
        .Frame(width: 3, height: CardHeight - 28)
        .Background(Colors.Accent, 1.5f)
        .Padding(new Insets(14, 0, 0, 0))
        .Hidden(() => !At(row.Value, column).Selected.Value),
      Button(
          Image(ElementIcons.Of(UiIconKind.Edit), 20).Foreground(Colors.Muted),
          () => At(row.Value, column).Edit,
          ButtonStyle.Icon
        )
        .Frame(width: 36, height: 36)
        .Padding(10)
        .Frame(maxWidth: float.PositiveInfinity, alignment: Alignment.TopTrailing)
    );
}
