using System.Collections.Generic;
using ElementUI;

namespace Element.Presentation.OverlayEditor.Canvas.AddMenu;

/// <summary>
/// The rows of the add menu, or of a list it opens beside it, as they were
/// when it opened. A row that can't add now is off and says why.
/// </summary>
internal sealed class AddMenuViewModel : ViewModel
{
  public AddMenuViewModel(IReadOnlyList<AddMenuRow> rows, bool large)
  {
    Large = large;
    AddMenuItemViewModel[] items = new AddMenuItemViewModel[rows.Count];
    for (int i = 0; i < items.Length; i++)
      items[i] = Own(new AddMenuItemViewModel(rows[i]));
    Items = items;
  }

  public readonly IReadOnlyList<AddMenuItemViewModel> Items;

  /// <summary>The add menu itself, with larger rows than the lists beside it.</summary>
  public readonly bool Large;
}
