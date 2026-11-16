using Element.Presentation.Settings.Window;
using ElementUI;
using static Element.Presentation.Ui.Theme.UiLocale;

namespace Element.Presentation.Settings.Pages;

/// <summary>The first page: four categories, each a card that opens its page or the overlay editor.</summary>
internal sealed class HomeModel : SettingsPageModel
{
  public readonly bool HasProfiles;
  public Command EditOverlay { get; }
  public Command OpenTweaks { get; }
  public Command OpenEffects { get; }
  public Command OpenGeneral { get; }

  public HomeModel(SettingsWindowViewModel window)
    : base(window, null, L("설정", "Settings"))
  {
    HasProfiles = window.Features.Profiles != null;
    EditOverlay = Command(window.EditLayout);
    OpenTweaks = Command(() => window.Show(window.Tweaks));
    OpenEffects = Command(() => window.Show(window.Effects));
    OpenGeneral = Command(() => window.Show(window.General));
  }
}
