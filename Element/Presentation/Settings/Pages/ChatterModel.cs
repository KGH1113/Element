using Element.Presentation.Settings.Window;
using ElementUI;
using static Element.Presentation.Ui.Theme.UiLocale;

namespace Element.Presentation.Settings.Pages;

// Placeholder while this page moves to ElementUI; the old page is in git history (dev).
internal sealed class ChatterModel : SettingsPageModel
{
  public ChatterModel(SettingsWindowViewModel window, SettingsPageModel parent)
    : base(window, parent, L("채터 블로커", "Chatter blocker")) { }
}
