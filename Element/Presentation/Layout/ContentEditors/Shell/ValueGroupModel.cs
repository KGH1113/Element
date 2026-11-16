using System.Collections.Generic;

namespace Element.Presentation.Layout.ContentEditors.Shell;

/// <summary>A row of the dock's values: a heading, or none, and the chips beside it.</summary>
internal sealed class ValueGroupModel
{
  public ValueGroupModel(string heading, IReadOnlyList<ValueChipModel> chips)
  {
    Heading = heading;
    Chips = chips;
  }

  /// <summary>Beside the chips; null when the place has only this row.</summary>
  public readonly string Heading;

  public readonly IReadOnlyList<ValueChipModel> Chips;
}
