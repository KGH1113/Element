using System.Collections.Generic;
using Element.Presentation.OverlayEditor.Parts.Property;
using ElementUI;

namespace Element.Presentation.OverlayEditor.TextEditor;

/// <summary>
/// What the Animate tool shows in the property column: a switch for values
/// growing each time they change.
/// </summary>
internal sealed class TextAnimateViewModel : ViewModel
{
  public TextAnimateViewModel(PropertyButtonViewModel grow) => Rows = new[] { Own(grow) };

  /// <summary>The tool's switches, as property rows.</summary>
  public readonly IReadOnlyList<PropertyButtonViewModel> Rows;
}
