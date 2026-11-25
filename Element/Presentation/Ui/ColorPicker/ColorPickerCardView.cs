using Element.Presentation.Ui.Declarative.Controls;
using ElementUI;
using UnityEngine;
using static ElementUI.UI;
using Font = ElementUI.Font;

namespace Element.Presentation.Ui.ColorPicker;

/// <summary>
/// The picker as a card: its title with a close button, Highlight/Custom for
/// an overlay color, then the square, hue and opacity bars and HEX with the
/// opacity or Default, or following the highlight only the opacity; a line
/// under them says what can't be read. For popovers and the settings' swatches.
/// </summary>
internal sealed class ColorPickerCardView : ColorPickerLayout
{
  public const float Width = 288;
  private static readonly UiColor Card = new Color(.105f, .105f, .105f);

  public ColorPickerCardView(ColorPickerViewModel vm, ColorPickerParts parts = null)
    : base(vm, parts) { }

  public override View Body =>
    VStack(
        spacing: 0,
        HStack(
            UI.Text(() => VM.Title.Value)
              .Font(Font.Body)
              .LineLimit(1)
              .Named("Color picker title")
              .Frame(maxWidth: float.PositiveInfinity, alignment: Alignment.Leading),
            CloseButton()
          )
          .Frame(height: 40)
          .Padding(new Insets(6, 16, 0, 12)),
        VStack(
            spacing: 0,
            If(() => VM.SourceShown.Value, () => Source().Padding(new Insets(6, 0, 2, 0))),
            If(() => VM.Custom.Value, OwnColor, HighlightColor)
          )
          .Padding(new Insets(0, 16, 5, 16))
      )
      .Frame(maxWidth: Width)
      .Background(Card, 8);

  // The color's own controls; Default sits beside HEX for an opaque color that has one.
  private View OwnColor() =>
    VStack(
      spacing: 0,
      Plane().Padding(new Insets(6, 0, 0, 0)),
      Hue().Padding(new Insets(8, 0, 0, 0)),
      If(() => VM.OpacityShown.Value, () => Opacity().Padding(new Insets(4, 0, 4, 0))),
      HexRow().Padding(new Insets(6, 0, 0, 0)),
      Error(16).Padding(new Insets(3, 0, 0, 0))
    );

  // Following the highlight leaves only its opacity.
  private View HighlightColor() =>
    VStack(
      spacing: 0,
      Opacity().Padding(new Insets(8, 0, 0, 0)),
      OpacityRow().Padding(new Insets(10, 0, 0, 0)),
      Error(16).Padding(new Insets(3, 0, 0, 0))
    );
}
