using ElementUI;
using static Element.Presentation.Ui.Theme.UiLocale;
using static ElementUI.UI;

namespace Element.Presentation.Settings.Pages;

internal sealed class JudgmentView : View<JudgmentModel>
{
  public JudgmentView(JudgmentModel vm)
    : base(vm) { }

  public override View Body =>
    VStack(
      spacing: 12,
      // The buttons sit beside the hint on a wide window and under it on a narrow one.
      ViewThatFits(
        HStack(spacing: 12, Hint().Frame(maxWidth: float.PositiveInfinity, alignment: Alignment.Leading), Actions())
          .Frame(minWidth: 600, maxWidth: float.PositiveInfinity),
        VStack(spacing: 8, Hint(), Actions())
      ),
      // Three columns where three fit; 226 keeps the widest window (936) at three.
      Grid(minColumnWidth: 212, spacing: 12, maxColumns: 3, Tiles())
    );

  private View Hint() =>
    Text(() =>
        VM.TextLinked
          ? L(
            "표시할 판정을 선택하세요. 얼불춤의 판정 텍스트 숨기기와 함께 바뀌어요.",
            "Choose the popups to show. Synced with ADOFAI's Hide Judgment Text."
          )
          : L(
            "표시할 판정을 선택하세요. 경쟁 모드를 켜면 얼불춤 설정과 함께 바뀌어요.",
            "Choose the popups to show. Turn on Competitive Mode to sync with ADOFAI."
          )
      )
      .Font(Font.Callout)
      .Foreground(Colors.Muted);

  private View Actions() =>
    HStack(
      spacing: 12,
      Button(Text(L("모두 표시", "Show all")), () => VM.ShowAll),
      Button(Text(L("모두 숨김", "Hide all")), () => VM.HideAll)
    );

  private View[] Tiles()
  {
    View[] tiles = new View[VM.Choices.Length];
    for (int i = 0; i < tiles.Length; i++)
      tiles[i] = Tile(VM.Choices[i]);
    return tiles;
  }

  /// <summary>A judgment's tile: its color, its name, and whether it shows. A click flips it.</summary>
  private static View Tile(JudgmentChoice choice) =>
    Button(
      HStack(
          spacing: 12,
          VAlignment.Top,
          Spacer().Frame(width: 8, height: 8).Background(choice.Tint, 4).Padding(new Insets(7, 0, 0, 0)),
          VStack(
            spacing: 6,
            Text(choice.Name).Foreground(() => choice.Hidden.Value ? Colors.Muted : Colors.Foreground),
            Text(() => choice.Hidden.Value ? L("숨김", "Hidden") : L("표시", "Shown"))
              .Font(Font.Caption)
              .Foreground(() => choice.Hidden.Value ? Colors.Muted : Colors.Accent)
          )
        )
        .Frame(maxWidth: float.PositiveInfinity, alignment: Alignment.TopLeading),
      () => choice.Toggle,
      ButtonStyle.Card
    );
}
