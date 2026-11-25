using System;
using System.Collections.Generic;
using Element.Domain.Overlay.Values;
using ElementUI;

namespace Element.Presentation.OverlayEditor.Parts.ValueSource;

/// <summary>
/// The values a tool can read, as its menu opens: the ones in the text
/// first, then a way to pick any value from the dock.
/// </summary>
internal sealed class ValueSourceMenuViewModel : ViewModel
{
  public ValueSourceMenuViewModel(
    IReadOnlyList<OverlayValueDefinition> values,
    string followed,
    Action<OverlayValueDefinition> picked,
    Action other
  )
  {
    ValueSourceItemViewModel[] items = new ValueSourceItemViewModel[values.Count];
    for (int i = 0; i < items.Length; i++)
      items[i] = Own(new ValueSourceItemViewModel(values[i], values[i].Key == followed, picked));
    Items = items;
    Other = Command(other);
  }

  /// <summary>The values in the text, in order.</summary>
  public readonly IReadOnlyList<ValueSourceItemViewModel> Items;

  /// <summary>Pick any value from the dock instead.</summary>
  public Command Other { get; }
}
