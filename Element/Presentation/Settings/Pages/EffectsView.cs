using Element.Presentation.Ui.Declarative;
using Element.Presentation.Ui.Graphics;
using ElementUI;
using static Element.Presentation.Ui.Declarative.Controls.SwitchCards;
using static Element.Presentation.Ui.Theme.UiLocale;
using static ElementUI.UI;

namespace Element.Presentation.Settings.Pages;

/// <summary>Effects: the removal switch, the preset cards in three, two or one columns, and when changes apply.</summary>
internal sealed class EffectsView : View<EffectsModel>
{
  internal const float CardHeight = 140;

  public EffectsView(EffectsModel vm)
    : base(vm) { }

  public override View Body =>
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
              Text(L("프리셋 추가", "New preset")).Frame(maxWidth: float.PositiveInfinity),
              () => VM.NewPreset,
              ButtonStyle.Prominent
            )
            .Frame(width: 156, height: 44)
        )
        .Padding(new Insets(4, 0, 4, 0)),
      If(
        () => VM.HasPresets.Value,
        // Three columns where three fit; 226 keeps the widest window (936) at three.
        () => Grid(minColumnWidth: 192, spacing: 12, maxColumns: 3, ForEach(() => VM.Cards, card => card.Id, Card)),
        () =>
          Text(L("첫 프리셋을 만들어 보세요.", "Create your first preset."))
            .Font(Font.Heading)
            .Foreground(Colors.Muted)
            .Padding(new Insets(16, 0, 16, 0))
      ),
      Text(
          L(
            "변경 사항은 레벨을 시작하거나 재시작할 때 적용됩니다.",
            "Changes take effect when a level starts or restarts."
          )
        )
        .Font(Font.Caption)
        .Foreground(Colors.Muted)
    );

  /// <summary>A preset's card: click to apply; the pencil edits it; a bar on the left marks the selected one.</summary>
  private static View Card(ItemSlot<PresetCardModel> card) =>
    ZStack(
      Alignment.TopLeading,
      Button(
          VStack(
              spacing: 6,
              Text(() => card.Value.Name)
                .Font(Font.Heading.Sized(20))
                .Foreground(Colors.Foreground)
                .LineLimit(2)
                .Frame(maxHeight: 54, alignment: Alignment.TopLeading)
                .Padding(new Insets(0, 0, 0, 36)),
              Spacer(),
              HStack(
                spacing: 12,
                Text(() => card.Value.Removes)
                  .LineLimit(1)
                  .Frame(maxWidth: float.PositiveInfinity, alignment: Alignment.Leading),
                Text(() => card.Value.Shortcut)
              ),
              Text(() => card.Value.State.Value)
                .Foreground(() => card.Value.Selected.Value ? Colors.Accent : Colors.Muted)
            )
            .Font(Font.Caption)
            .Foreground(Colors.Muted)
            .Frame(
              maxWidth: float.PositiveInfinity,
              maxHeight: float.PositiveInfinity,
              alignment: Alignment.TopLeading
            ),
          () => card.Value.Apply,
          ButtonStyle.Card
        )
        .Frame(height: CardHeight),
      Spacer()
        .Frame(width: 3, height: CardHeight - 28)
        .Background(Colors.Accent, 1.5f)
        .Padding(new Insets(14, 0, 0, 0))
        .Hidden(() => !card.Value.Selected.Value),
      Button(
          Image(ElementIcons.Of(UiIconKind.Edit), 20).Foreground(Colors.Muted),
          () => card.Value.Edit,
          ButtonStyle.Icon
        )
        .Frame(width: 36, height: 36)
        .Padding(10)
        .Frame(maxWidth: float.PositiveInfinity, alignment: Alignment.TopTrailing)
    );
}
