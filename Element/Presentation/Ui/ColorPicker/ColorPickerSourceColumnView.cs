using ElementUI;
using static ElementUI.UI;

namespace Element.Presentation.Ui.ColorPicker;

/// <summary>
/// An overlay color that can only be solid, laid out borderless in one
/// column, such as a stop of a color that follows a value: Highlight/Custom
/// on top, then the square, sliders and HEX with opacity, or only the opacity
/// while it follows the highlight. Whoever places it names the color.
/// </summary>
internal sealed class ColorPickerSourceColumnView : ColorPickerLayout
{
  public const float SourceGap = 12,
    Height = ColorPickerColumnsView.Height + 36 + SourceGap;

  public ColorPickerSourceColumnView(ColorPickerModel vm, ColorPickerParts parts)
    : base(vm, parts) { }

  public override View Body =>
    VStack(spacing: SourceGap, Source(), If(() => VM.Custom.Value, OwnColumn, HighlightColumn))
      .Frame(width: Column, height: Height, alignment: Alignment.Top);
}
