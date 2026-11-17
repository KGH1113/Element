using System.Collections.Generic;

namespace Element.Presentation.OverlayEditor.Parts.Dock.ValueBrowser;

/// <summary>A row of the dock's values: a heading, or none, and the chips beside it. Fixed once made.</summary>
internal sealed class ValueChipRow
{
  public ValueChipRow(string heading, IReadOnlyList<ValueChipViewModel> chips)
  {
    Heading = heading;
    Chips = chips;
  }

  /// <summary>Beside the chips; null when the place has only this row.</summary>
  public readonly string Heading;

  public readonly IReadOnlyList<ValueChipViewModel> Chips;
}
