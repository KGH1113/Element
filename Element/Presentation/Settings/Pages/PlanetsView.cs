using System;
using System.Linq.Expressions;
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
      ZStack(
        Alignment.TopLeading,
        new WidthReader(Bind(() => VM.Width)),
        VStack(
          spacing: 0,
          When(
            () => VM.Width < 600,
            () => Preview().Frame(maxWidth: float.PositiveInfinity, height: 168).Padding(new Insets(0, 0, 12, 0))
          ),
          HStack(
            spacing: 0,
            VAlignment.Top,
            When(
              () => VM.Width >= 760,
              () => Preview().Frame(width: 268, height: ColumnHeight).Padding(new Insets(0, 0, 0, 16))
            ),
            When(
              () => VM.Width >= 600 && VM.Width < 760,
              () => Preview().Frame(width: 200, height: ColumnHeight).Padding(new Insets(0, 0, 0, 16))
            ),
            Rows().Frame(maxWidth: float.PositiveInfinity)
          )
        )
      )
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
  /// its color and opacity. On a very narrow window the buttons go under the name.
  /// </summary>
  private View Part(int index)
  {
    // The part's name and picker title are the same on every planet.
    PlanetPartModel kind = VM.Fire.Parts[index];
    View source = Button(
        Text(() =>
            VM.Picked.Value.Parts[index].Appearance.OverrideColor
              ? L("직접 지정", "Custom color")
              : L("맵 색상", "Map color")
          )
          .Frame(maxWidth: float.PositiveInfinity),
        () => VM.Picked.Value.Parts[index].ToggleSource
      )
      .Frame(width: 144);
    View color = ColorSwatch
      .Of(
        kind.ColorTitle,
        Bind(() => VM.Picked.Value.Parts[index].Color),
        opacity: true,
        caption: Text(() => VM.Picked.Value.Parts[index].Appearance.Opacity, format: "{0}%", number: "0")
      )
      .Frame(width: 108);
    return VStack(
        spacing: 0,
        When(() => VM.Width < 400, () => Text(kind.Name).Padding(new Insets(0, 0, 8, 0))),
        HStack(
          spacing: 0,
          When(
            () => VM.Width >= 400,
            () =>
              Text(kind.Name)
                .Frame(maxWidth: float.PositiveInfinity, alignment: Alignment.Leading)
                .Padding(new Insets(0, 0, 0, 12))
          ),
          source,
          color.Padding(new Insets(0, 12, 0, 0))
        )
      )
      .Disabled(() => !VM.Settings.Gameplay.Planets.Enabled || !VM.Picked.Value.Profile.Enabled)
      .Padding(10, 16)
      .Frame(maxWidth: float.PositiveInfinity, alignment: Alignment.Leading)
      .Background(Colors.Surface, 12);
  }

  /// <summary>
  /// <paramref name="content"/> while <paramref name="when"/> holds, else nothing. A stack
  /// lays out an <c>If</c>'s content itself, so a bare <c>If</c> switching doesn't lay
  /// the stack out again (ElementUI 11316be); in a frame it does. Empty, it still
  /// counts in its stack's spacing, so these stacks space with padding instead.
  /// </summary>
  private static View When(Expression<Func<bool>> when, Func<View> content) => If(when, content).Frame();
}
