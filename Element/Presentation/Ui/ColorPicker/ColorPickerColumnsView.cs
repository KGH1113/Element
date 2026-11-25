using Element.Presentation.Ui.Declarative;
using Element.Presentation.Ui.Declarative.Controls;
using Element.Presentation.Ui.Graphics;
using Element.Presentation.Ui.Theme;
using ElementUI;
using static Element.Presentation.Ui.Theme.UiLocale;
using static ElementUI.UI;
using Font = ElementUI.Font;

namespace Element.Presentation.Ui.ColorPicker;

/// <summary>
/// The picker laid out borderless for the panel beside it, such as the
/// keypad below it: no card, title or close; whoever places it names the
/// color and closes it. An overlay element's own color takes two columns: the
/// color on the left (the square, hue and opacity), and on the right
/// Highlight/Custom, the color bar once it is a gradient, and its numbers.
/// Following the highlight there is no color to pick, so only the right column
/// stays, with the opacity bar. Other colors take one column.
/// </summary>
internal sealed class ColorPickerColumnsView : ColorPickerLayout
{
  public const float Gap = 20,
    TwoColumnWidth = Column * 2 + Gap,
    Height = 290;

  private const float BarsHeight = ColorSliderView.Height * 2 + 8,
    PlaneHeight = Height - BarsHeight,
    ButtonHeight = 36;

  private readonly bool _two;
  private readonly float _width;

  /// <param name="two">With the color bar: an overlay element's color.</param>
  /// <param name="width">One column's width; its controls keep the color square's and center in it.</param>
  public ColorPickerColumnsView(ColorPickerViewModel vm, ColorPickerParts parts, bool two, float width)
    : base(vm, parts)
  {
    _two = two;
    _width = width;
  }

  public override View Body =>
    _two
      ? HStack(
          spacing: 0,
          alignment: VAlignment.Top,
          If(
            () => VM.Custom.Value,
            () =>
              OwnColor()
                .Frame(width: Column, height: Height, alignment: Alignment.Top)
                .Padding(new Insets(0, 0, 0, Gap))
          ),
          // One column of controls in both cases: the color bar and the numbers live in it.
          Controls().Frame(maxWidth: float.PositiveInfinity, alignment: Alignment.Top)
        )
        .Frame(maxWidth: float.PositiveInfinity, alignment: Alignment.TopLeading)
      : OwnColumn().Frame(width: Column).Frame(width: _width, height: Height, alignment: Alignment.Top);

  // A color of its own: the square, large, over its hue and opacity.
  private View OwnColor() => VStack(spacing: 0, Plane(PlaneHeight), Hue().Padding(new Insets(8, 0, 0, 0)), Opacity());

  // One column from the top; each part brings its own gap, so one that isn't shown leaves none.
  private View Controls() =>
    VStack(
      spacing: 0,
      Source(),
      If(
        () => VM.GradientOn.Value,
        () =>
          new HostedRect(
            Parts.Gradient.Root,
            Parts.Park,
            float.NaN,
            () => VM.BarHeight.Value,
            Parts.Gradient.Layout
          ).Padding(new Insets(10, 0, 0, 0))
      ),
      If(
        () => VM.Custom.Value,
        () =>
          VStack(
              spacing: 0,
              HexField().Frame(maxWidth: float.PositiveInfinity, height: FieldHeight),
              If(() => VM.Error.Value.Length > 0, () => Error(14).Padding(new Insets(4, 0, 0, 0)))
            )
            .Padding(new Insets(12, 0, 0, 0)),
        () => Opacity().Padding(new Insets(10, 0, 0, 0))
      ),
      HStack(
          spacing: 8,
          WideOpacityBox(),
          If(
            () => VM.PositionShown.Value,
            () =>
              new HostedRect(Parts.Gradient.PositionSlot, Parts.Park, float.NaN, FieldHeight).Frame(
                maxWidth: float.PositiveInfinity,
                height: FieldHeight
              )
          )
        )
        .Frame(maxWidth: float.PositiveInfinity, height: FieldHeight)
        .Padding(new Insets(8, 0, 0, 0)),
      If(() => !VM.GradientOn.Value, () => MakeGradient().Padding(new Insets(10, 0, 0, 0)))
    );

  private View MakeGradient() =>
    Button(
        HStack(
            spacing: 6,
            Image(ElementIcons.Of(UiIconKind.Plus), 14).Foreground(Colors.Muted),
            UI.Text(L("그라디언트 만들기", "Make a gradient")).Font(Font.Caption.Sized(13)).LineLimit(1)
          )
          .Frame(maxWidth: float.PositiveInfinity, height: ButtonHeight),
        () => VM.AddGradient,
        ButtonStyle.Plain
      )
      .ButtonStyle(new ButtonSurfaceStyle(UiPalette.EditorControl, ButtonHeight / 2))
      .Named("Make gradient");
}
