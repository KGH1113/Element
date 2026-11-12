using ElementUI;
using static ElementUI.UI;

namespace Element.Presentation.Settings.Pages;

internal sealed class JudgmentView : View<JudgmentModel>
{
  public JudgmentView(JudgmentModel vm)
    : base(vm) { }

  public override View Body => Text("Judgment");
}
