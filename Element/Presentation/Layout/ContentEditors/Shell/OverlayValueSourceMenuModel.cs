using System;
using System.Collections.Generic;
using Element.Domain.Overlay.Values;
using ElementUI;

namespace Element.Presentation.Layout.ContentEditors.Shell;

/// <summary>
/// The values a color can follow, as its menu opens: the ones in the text
/// first, then a way to pick any value from the dock.
/// </summary>
internal sealed class OverlayValueSourceMenuModel : ViewModel
{
  public OverlayValueSourceMenuModel(
    IReadOnlyList<OverlayValueDefinition> values,
    string followed,
    Action<OverlayValueDefinition> picked,
    Action other
  )
  {
    OverlayValueSourceItemModel[] items = new OverlayValueSourceItemModel[values.Count];
    for (int i = 0; i < items.Length; i++)
      items[i] = Own(new OverlayValueSourceItemModel(values[i], values[i].Key == followed, picked));
    Items = items;
    Other = Command(other);
  }

  /// <summary>The values in the text, in order.</summary>
  public readonly IReadOnlyList<OverlayValueSourceItemModel> Items;

  /// <summary>Pick any value from the dock instead.</summary>
  public Command Other { get; }
}
