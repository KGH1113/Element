using Element.Presentation.Ui.Declarative;
using Element.Presentation.Ui.Declarative.Controls;
using Element.Presentation.Ui.Theme;
using ElementUI;
using static ElementUI.UI;

namespace Element.Presentation.OverlayEditor.Canvas.AddMenu;

/// <summary>
/// The add menu's rows, or a list's beside it: each an icon in the highlight
/// color, its name, and what it adds or why it can't be added now.
/// </summary>
internal sealed class AddMenuView : View<AddMenuViewModel>
{
  public AddMenuView(AddMenuViewModel vm)
    : base(vm) { }

  public override View Body =>
    VStack(spacing: 4, ForEach(VM.Items, item => item.Row.Name, slot => new AddMenuItemView(slot, VM.Large)))
      .Padding(8);
}
