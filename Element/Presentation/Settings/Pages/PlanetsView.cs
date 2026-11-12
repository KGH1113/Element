using ElementUI;
using static ElementUI.UI;

namespace Element.Presentation.Settings.Pages;

internal sealed class PlanetsView : View<PlanetsModel>
{
  public PlanetsView(PlanetsModel vm)
    : base(vm) { }

  public override View Body => Text("Planets");
}
