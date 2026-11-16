using System.Collections.Generic;
using Element.Presentation.Layout.Kinds;
using ElementUI;

namespace Element.Presentation.Layout.Canvas;

/// <summary>
/// The rows of the add menu, or of a list it opens beside it, as they were
/// when it opened. A row that can't add now is off and says why.
/// </summary>
internal sealed class AddMenuListModel : ViewModel
{
  public AddMenuListModel(IReadOnlyList<AddMenuRow> rows, bool large)
  {
    Large = large;
    AddMenuItemModel[] items = new AddMenuItemModel[rows.Count];
    for (int i = 0; i < items.Length; i++)
      items[i] = Own(new AddMenuItemModel(rows[i]));
    Items = items;
  }

  public readonly IReadOnlyList<AddMenuItemModel> Items;

  /// <summary>The add menu itself, with larger rows than the lists beside it.</summary>
  public readonly bool Large;
}

/// <summary>One row: what it adds, and adding it.</summary>
internal sealed class AddMenuItemModel : ViewModel
{
  public AddMenuItemModel(AddMenuRow row)
  {
    Row = row;
    Add = Command(() => row.Pressed(), canExecute: Computed(() => Row.Enabled));
  }

  public readonly AddMenuRow Row;

  public Command Add { get; }
}
