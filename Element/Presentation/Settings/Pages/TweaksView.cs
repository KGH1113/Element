using ElementUI;
using static ElementUI.UI;

namespace Element.Presentation.Settings.Pages;

internal sealed class TweaksView : View<TweaksModel>
{
  public TweaksView(TweaksModel vm)
    : base(vm) { }

  public override View Body => Text("Tweaks");
}
