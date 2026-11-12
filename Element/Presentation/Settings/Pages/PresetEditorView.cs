using ElementUI;
using static ElementUI.UI;

namespace Element.Presentation.Settings.Pages;

internal sealed class PresetEditorView : View<PresetEditorModel>
{
  public PresetEditorView(PresetEditorModel vm)
    : base(vm) { }

  public override View Body => Text("PresetEditor");
}
