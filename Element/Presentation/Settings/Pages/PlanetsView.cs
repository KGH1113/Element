using Element.Presentation.Ui.Declarative.Controls;
using ElementUI;
using static Element.Presentation.Ui.Theme.UiLocale;
using static ElementUI.UI;

namespace Element.Presentation.Settings.Pages;

/// <summary>
/// Custom planets on or off, a tab per planet, and for the picked planet its
/// preview beside its body, trail and ring. The rows edit whichever planet is
/// picked. A narrow window puts the preview above the rows, and on a very
/// narrow one each row's buttons go under its name.
/// </summary>
internal sealed class PlanetsView : View<PlanetsModel>
{
  // The rows beside the preview: the planet's switch and three part rows.
  private const float ColumnHeight = 56 + 3 * 60 + 3 * 8;

  public PlanetsView(PlanetsModel vm)
    : base(vm) { }

  public override View Body =>
    VStack(
      spacing: 12,
      HStack(
          spacing: 16,
          VStack(
            spacing: 4,
            Text(L("행성 꾸미기", "Custom planets")),
            Text(L("행성별 색상과 불투명도를 직접 설정합니다.", "Set color and opacity for each planet."))
              .Font(Font.Caption)
              .Foreground(Colors.Muted)
          ),
          Spacer(),
          Toggle("", Bind(() => VM.Settings.Gameplay.Planets.Enabled))
        )
        .Padding(20, 16)
        .Background(Colors.Surface, 12),
      Picker("", new[] { L("불", "Fire"), L("얼음", "Ice"), L("세 번째 공", "Third planet") }, Bind(() => VM.Planet)),
      ViewThatFits(
        Beside(268).Frame(minWidth: 760, maxWidth: float.PositiveInfinity),
        Beside(200).Frame(minWidth: 600, maxWidth: float.PositiveInfinity),
        VStack(spacing: 12, Preview().Frame(maxWidth: float.PositiveInfinity, height: 168), Rows())
      )
    );

  /// <summary>The preview, <paramref name="width"/> wide, beside the rows.</summary>
  private View Beside(float width) =>
    HStack(
      spacing: 16,
      VAlignment.Top,
      Preview().Frame(width: width, height: ColumnHeight),
      Rows().Frame(maxWidth: float.PositiveInfinity)
    );

  /// <summary>The picked planet as the game draws it, and what the preview shows.</summary>
  private View Preview() =>
    VStack(
        spacing: 8,
        HAlignment.Center,
        new TextureImage(() => VM.Preview).Frame(maxWidth: float.PositiveInfinity, maxHeight: float.PositiveInfinity),
        Text(() => VM.PreviewCaption.Value).Font(Font.Caption).Foreground(Colors.Muted).TextAlignment(TextAlign.Center)
      )
      .Padding(16)
      .Background(Colors.Surface, 12);

  /// <summary>Whether the picked planet's own colors apply, then its body, trail and ring; off while custom planets are off.</summary>
  private View Rows() =>
    VStack(
      spacing: 8,
      Toggle(L("이 행성에 적용", "Customize this planet"), Bind(() => VM.Picked.Value.Profile.Enabled))
        .Disabled(() => !VM.Settings.Gameplay.Planets.Enabled)
        .Frame(maxWidth: float.PositiveInfinity, height: 56)
        .Padding(0, 16)
        .Background(Colors.Surface, 12),
      Part(0),
      Part(1),
      Part(2)
    );

  /// <summary>
  /// A part of the picked planet: its name, where its color comes from, and
  /// its color and opacity. Where the row is too narrow the buttons go under the name.
  /// </summary>
  private View Part(int index)
  {
    // The part's name and picker title are the same on every planet.
    PlanetPartModel kind = VM.Fire.Parts[index];
    View Source() =>
      Button(
          Text(() =>
              VM.Picked.Value.Parts[index].Appearance.OverrideColor
                ? L("직접 지정", "Custom color")
                : L("맵 색상", "Map color")
            )
            .Frame(maxWidth: float.PositiveInfinity),
          () => VM.Picked.Value.Parts[index].ToggleSource
        )
        .Frame(width: 144);
    View Swatch() =>
      ColorSwatch
        .Of(
          kind.ColorTitle,
          Bind(() => VM.Picked.Value.Parts[index].Color),
          opacity: true,
          caption: Text(() => VM.Picked.Value.Parts[index].Appearance.Opacity, format: "{0}%", number: "0")
        )
        .Frame(width: 108);
    return ViewThatFits(
        // At least 352 wide, so every part makes the same choice whatever its name.
        HStack(
            spacing: 12,
            Text(kind.Name).Frame(maxWidth: float.PositiveInfinity, alignment: Alignment.Leading),
            Source(),
            Swatch()
          )
          .Frame(minWidth: 352, maxWidth: float.PositiveInfinity),
        VStack(spacing: 8, Text(kind.Name), HStack(spacing: 12, Source(), Swatch()))
      )
      .Disabled(() => !VM.Settings.Gameplay.Planets.Enabled || !VM.Picked.Value.Profile.Enabled)
      .Padding(10, 16)
      .Frame(maxWidth: float.PositiveInfinity, alignment: Alignment.Leading)
      .Background(Colors.Surface, 12);
  }
}
