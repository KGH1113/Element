using System.Collections.Generic;
using ElementUI;

namespace Element.Presentation.OverlayEditor.Canvas.AddMenu;

/// <summary>One row: what it adds, and adding it.</summary>
internal sealed class AddMenuItemViewModel : ViewModel
{
  public AddMenuItemViewModel(AddMenuRow row)
  {
    Row = row;
    Add = Command(() => row.Pressed(), canExecute: Computed(() => Row.Enabled));
  }

  public readonly AddMenuRow Row;

  public Command Add { get; }
}
