using ElementUI;
using static ElementUI.UI;

namespace Element.Presentation.Ui.ColorPicker;

/// <summary>
/// The picked color of a gradient, in the place of a keypad: the square over
/// its hue and opacity, as wide and as tall as that place. The gradient's
/// own controls (its color bar and numbers) stand in the column above it, so
/// nothing reaches out of that column. Following the highlight there is no
/// color to pick, so the place shows that color.
/// </summary>
internal sealed class ColorPickerPlaneView : ColorPickerLayout
{
  private const float Gap = 8;

  private readonly float _width,
    _height;

  public ColorPickerPlaneView(ColorPickerViewModel vm, ColorPickerParts parts, float width, float height)
    : base(vm, parts)
  {
    _width = width;
    _height = height;
  }

  public override View Body =>
    If(
        () => VM.Custom.Value,
        // In the input method's place, as a solid color is picked there: the hue standing left of the square, the opacity right.
        () =>
          HStack(
            spacing: Gap,
            UprightHue(_height),
            Plane(_height).Frame(maxWidth: float.PositiveInfinity),
            UprightOpacity(_height)
          ),
        () => Spacer().Frame(maxWidth: float.PositiveInfinity, height: _height).Background(() => VM.Swatch.Value, 12)
      )
      .Frame(width: _width, height: _height, alignment: Alignment.Top);
}
