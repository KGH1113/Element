using ElementUI;
using static ElementUI.UI;

namespace Element.Presentation.Settings.Pages;

internal sealed class GeneralView : View<GeneralModel>
{
  public GeneralView(GeneralModel vm)
    : base(vm) { }

  public override View Body => Text("General");
}
