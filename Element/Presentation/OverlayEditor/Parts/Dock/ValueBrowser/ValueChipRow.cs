using System.Collections.Generic;
using ElementUI;

namespace Element.Presentation.OverlayEditor.Parts.Dock.ValueBrowser;

/// <summary>
/// A row of the dock's values: a heading, or none, and the chips beside it.
/// Its chips are fixed once made; it shows while one of them fits.
/// </summary>
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

  /// <summary>One of its chips fits where the dock puts values now.</summary>
  public readonly State<bool> Shown = new(true);
}
