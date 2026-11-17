using Element.Presentation.Ui.Declarative.Controls;
using ElementUI;
using static Element.Presentation.Ui.Theme.UiLocale;
using static ElementUI.UI;
using Font = ElementUI.Font;

namespace Element.Presentation.Ui.ColorPicker;

/// <summary>
/// The picker laid out borderless for the panel beside it, such as the
/// keypad below it: no card, title or close; whoever places it names the
/// color and closes it. An overlay element's color takes two columns, the
/// color bar and Highlight/Custom on the left, the color's own controls on
/// the right, at one height whatever shows, so switching moves nothing.
/// Other colors take one column.
/// </summary>
internal sealed class ColorPickerColumnsView : ColorPickerLayout
{
  public const float Gap = 20,
    TwoColumnWidth = Column * 2 + Gap,
    Height = 290;

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
      ? HStack(spacing: Gap, alignment: VAlignment.Top, Left().Frame(width: Column), Right().Frame(width: Column))
        .Frame(width: TwoColumnWidth, height: Height, alignment: Alignment.TopLeading)
      : OwnColumn().Frame(width: Column).Frame(width: _width, height: Height, alignment: Alignment.Top);

  // The color bar with its pill and kinds, Highlight/Custom under it, and
  // while a gradient follows the highlight, a line saying what is left to set.
  private View Left() =>
    VStack(
      spacing: 0,
      new HostedRect(Parts.Gradient.Root, Parts.Park, float.NaN, () => VM.BarHeight.Value, Parts.Gradient.Layout),
      Source(),
      If(
        () => VM.NoteShown.Value,
        () =>
          UI.Text(
              L(
                "하이라이트 색 하나로 칠해요. 점마다 불투명도만 정해요.",
                "One highlight color: each stop sets only its opacity."
              )
            )
            .Font(Font.Caption.Sized(12))
            .Foreground(Colors.Muted)
            .Named("Highlight note")
            .Padding(new Insets(8, 0, 0, 0))
      )
    );

  private View Right() => If(() => VM.Custom.Value, OwnColumn, HighlightColumn);
}
