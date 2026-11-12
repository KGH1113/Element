using Element.Presentation.Settings.Window;
using ElementUI;
using static Element.Presentation.Ui.Theme.UiLocale;

namespace Element.Presentation.Settings.Pages;

// Placeholder while this page moves to ElementUI; the old page is in git history (dev).
internal sealed class EditorTweaksModel : SettingsPageModel
{
  public EditorTweaksModel(SettingsWindowViewModel window, SettingsPageModel parent)
    : base(window, parent, L("에디터", "Level editor")) { }
}
