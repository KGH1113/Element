using ElementUI;
using static ElementUI.UI;

namespace Element.Presentation.Ui.ColorPicker;

/// <summary>
/// A solid color picked in the place of a keypad: the square over its hue and
/// HEX, as wide and as tall as that place. Its opacity is a field of its own
/// elsewhere. Following the highlight there is no color to pick, so the note
/// says where it comes from instead.
/// </summary>
internal sealed class ColorPickerCompactView : ColorPickerLayout
{
  private const float Gap = 8;

  private readonly float _width,
    _height;

  public ColorPickerCompactView(ColorPickerViewModel vm, ColorPickerParts parts, float width, float height)
    : base(vm, parts)
  {
    _width = width;
    _height = height;
  }

  public override View Body =>
    If(
        () => VM.Custom.Value,
        () =>
          VStack(
            spacing: 0,
            Plane(_height - ColorSliderView.Height - FieldHeight - Gap * 2),
            Hue().Padding(new Insets(Gap, 0, 0, 0)),
            HexField().Frame(maxWidth: float.PositiveInfinity, height: FieldHeight).Padding(new Insets(Gap, 0, 0, 0)),
            If(() => VM.Error.Value.Length > 0, () => Error(14).Padding(new Insets(4, 0, 0, 0)))
          ),
        () => DockNote().Padding(new Insets(Gap, 0, 0, 0))
      )
      .Frame(width: _width, height: _height, alignment: Alignment.Top);
}
