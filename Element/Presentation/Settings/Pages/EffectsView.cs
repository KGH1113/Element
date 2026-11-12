using ElementUI;
using static ElementUI.UI;

namespace Element.Presentation.Settings.Pages;

internal sealed class EffectsView : View<EffectsModel>
{
  public EffectsView(EffectsModel vm)
    : base(vm) { }

  public override View Body => Text("Effects");
}
