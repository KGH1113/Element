using UnityEngine;

namespace Element.Presentation.Ui.ColorPicker;

/// <summary>
/// What a picker's owner makes and keeps, which the picker's screen shows
/// where they go: the box the owner fills with its own opacity control, and
/// the color bar. They wait in <see cref="Park"/>, hidden, while no screen shows them.
/// </summary>
internal sealed class ColorPickerParts
{
  public ColorPickerParts(Transform park, RectTransform opacitySlot, UiGradientBar gradient)
  {
    Park = park;
    OpacitySlot = opacitySlot;
    Gradient = gradient;
  }

  public readonly Transform Park;

  /// <summary>Where the owner's opacity box goes; see <see cref="ColorPickerViewModel.ExternalOpacity"/>.</summary>
  public readonly RectTransform OpacitySlot;

  /// <summary>The color bar of an overlay element's color, in the right column.</summary>
  public readonly UiGradientBar Gradient;
}
