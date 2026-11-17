using Element.Configuration.Gameplay;
using ElementUI;
using static Element.Presentation.Ui.Theme.UiLocale;

namespace Element.Presentation.Settings.Planets;

/// <summary>One planet on the Planets page: whether its own colors apply, and its body, trail and ring.</summary>
internal sealed class PlanetViewModel : ViewModel
{
  /// <summary>The planet as the game numbers it: 0 fire, 1 ice, 2 the third planet.</summary>
  public readonly int Index;

  public readonly PlanetAppearanceProfile Profile;

  /// <summary>Body, trail and ring, in the order the page lists them.</summary>
  public readonly PlanetPartViewModel[] Parts;

  /// <param name="profile">The planet's settings; they are copied in place, so this stays the one the game reads.</param>
  public PlanetViewModel(int index, PlanetAppearanceProfile profile)
  {
    Index = index;
    Profile = profile;
    Parts = new[]
    {
      Own(new PlanetPartViewModel(profile.Body, L("공", "Body"), L("공 색상", "Body color"))),
      Own(new PlanetPartViewModel(profile.Tail, L("꼬리", "Trail"), L("꼬리 색상", "Trail color"))),
      Own(new PlanetPartViewModel(profile.Ring, L("고리", "Ring"), L("고리 색상", "Ring color"))),
    };
  }
}
