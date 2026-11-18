using Element.Presentation.Ui.Declarative.Controls;
using ElementUI;
using static Element.Presentation.Ui.Theme.UiLocale;
using static ElementUI.UI;
using Font = ElementUI.Font;

namespace Element.Presentation.Ui.ColorPicker;

/// <summary>
/// The picker laid out borderless for the panel beside it, such as the
/// keypad below it: no card, title or close; whoever places it names the
/// color and closes it. An overlay element's color takes two columns at one
/// height whatever shows, so switching moves nothing: on the left the color
/// itself (the square, hue and opacity), on the right what it is made of
/// (Highlight/Custom, the color bar with its kinds) and its numbers (HEX,
/// opacity and the picked color's position). Other colors take one column.
/// </summary>
internal sealed class ColorPickerColumnsView : ColorPickerLayout
{
  public const float Gap = 20,
    TwoColumnWidth = Column * 2 + Gap,
    Height = 290;

  private const float BarsHeight = ColorSliderView.Height * 2 + 8,
    PlaneHeight = Height - BarsHeight;

  private readonly bool _two;
  private readonly float _width;

  /// <param name="two">Two columns, with the color bar: an overlay element's color.</param>
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
          spacing: Gap,
          alignment: VAlignment.Top,
          If(() => VM.Custom.Value, OwnColor, FollowedColor)
            .Frame(width: Column, height: Height, alignment: Alignment.Top),
          Controls().Frame(width: Column, height: Height, alignment: Alignment.Top)
        )
        .Frame(width: TwoColumnWidth, height: Height, alignment: Alignment.TopLeading)
      : OwnColumn().Frame(width: Column).Frame(width: _width, height: Height, alignment: Alignment.Top);

  // A color of its own: the square, large, over its hue and opacity.
  private View OwnColor() => VStack(spacing: 0, Plane(PlaneHeight), Hue().Padding(new Insets(8, 0, 0, 0)), Opacity());

  // Following the highlight: the color as it is, what is left to set, and the opacity, where they stand above.
  private View FollowedColor() =>
    VStack(
      spacing: 0,
      RoundedRectangle(8).Fill(() => VM.Swatch.Value).Frame(maxWidth: float.PositiveInfinity, height: PlaneHeight),
      UI.Text(L("하이라이트 색을 따라가요. 불투명도만 정해요.", "Follows your highlight color. Only opacity is yours."))
        .Font(Font.Caption.Sized(12))
        .Foreground(Colors.Muted)
        .LineLimit(1)
        .Named("Highlight note")
        .Frame(maxWidth: float.PositiveInfinity, height: ColorSliderView.Height + 8, alignment: Alignment.Leading),
      Opacity()
    );

  // What the color is made of above, its numbers along the bottom.
  private View Controls() =>
    VStack(
      spacing: 0,
      Source(),
      new HostedRect(
        Parts.Gradient.Root,
        Parts.Park,
        float.NaN,
        () => VM.BarHeight.Value,
        Parts.Gradient.Layout
      ).Padding(new Insets(10, 0, 0, 0)),
      Spacer(),
      If(
        () => VM.Custom.Value,
        () => VStack(spacing: 4, Error(14), HexField().Frame(maxWidth: float.PositiveInfinity, height: FieldHeight))
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
        .Padding(new Insets(8, 0, 0, 0))
    );
}
