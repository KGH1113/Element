using ElementUI;
using static ElementUI.UI;

namespace Element.Presentation.Settings.Pages;

internal sealed class ChatterView : View<ChatterModel>
{
  public ChatterView(ChatterModel vm)
    : base(vm) { }

  public override View Body => Text("Chatter");
}
