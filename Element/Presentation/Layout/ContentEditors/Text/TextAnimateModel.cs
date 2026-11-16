using System.Collections.Generic;
using Element.Presentation.Layout.ContentEditors.Shell;
using ElementUI;

namespace Element.Presentation.Layout.ContentEditors.Text;

/// <summary>
/// What the Animate tool shows in the property column: a switch for values
/// growing each time they change, a switch for the color following a value,
/// and, while that is on, the color's controls.
/// </summary>
internal sealed class TextAnimateModel : ViewModel
{
  public TextAnimateModel(OverlayBarButtonModel grow, OverlayBarButtonModel color, OverlayValueColorModel valueColor)
  {
    Rows = new[] { Own(grow), Own(color) };
    ValueColor = Own(valueColor);
  }

  /// <summary>The two switches, as property rows.</summary>
  public readonly IReadOnlyList<OverlayBarButtonModel> Rows;

  public readonly OverlayValueColorModel ValueColor;

  /// <summary>The color follows a value in the range (all of it or some): its controls show.</summary>
  public readonly State<bool> ColorShown = new(false);
}
