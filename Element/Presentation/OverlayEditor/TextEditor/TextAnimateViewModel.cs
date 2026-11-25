using System.Collections.Generic;
using Element.Presentation.OverlayEditor.Parts.Property;
using Element.Presentation.OverlayEditor.Parts.ValueColor;
using ElementUI;

namespace Element.Presentation.OverlayEditor.TextEditor;

/// <summary>
/// What the Animate tool shows in the property column: a switch for values
/// growing each time they change, a switch for the color following a value,
/// and, while that is on, the color's controls.
/// </summary>
internal sealed class TextAnimateViewModel : ViewModel
{
  public TextAnimateViewModel(
    PropertyButtonViewModel grow,
    PropertyButtonViewModel color,
    ValueColorViewModel valueColor
  )
  {
    Rows = new[] { Own(grow), Own(color) };
    ValueColor = Own(valueColor);
  }

  /// <summary>The two switches, as property rows.</summary>
  public readonly IReadOnlyList<PropertyButtonViewModel> Rows;

  public readonly ValueColorViewModel ValueColor;

  /// <summary>The color follows a value in the range (all of it or some): its controls show.</summary>
  public readonly State<bool> ColorShown = new(false);
}
