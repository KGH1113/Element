using ElementUI;
using static ElementUI.UI;

namespace Element.Presentation.Settings.Pages;

internal sealed class EditorTweaksView : View<EditorTweaksModel>
{
  public EditorTweaksView(EditorTweaksModel vm)
    : base(vm) { }

  public override View Body => Text("EditorTweaks");
}
