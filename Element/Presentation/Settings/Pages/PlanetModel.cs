using Element.Configuration.Gameplay;
using ElementUI;
using static Element.Presentation.Ui.Theme.UiLocale;

namespace Element.Presentation.Settings.Pages;

/// <summary>One planet on the Planets page: whether its own colors apply, and its body, trail and ring.</summary>
internal sealed class PlanetModel : ViewModel
{
  /// <summary>The planet as the game numbers it: 0 fire, 1 ice, 2 the third planet.</summary>
  public readonly int Index;

  public readonly PlanetAppearanceProfile Profile;

  /// <summary>Body, trail and ring, in the order the page lists them.</summary>
  public readonly PlanetPartModel[] Parts;

  /// <param name="profile">The planet's settings; they are copied in place, so this stays the one the game reads.</param>
  public PlanetModel(int index, PlanetAppearanceProfile profile)
  {
    Index = index;
    Profile = profile;
    Parts = new[]
    {
      Own(new PlanetPartModel(profile.Body, L("공", "Body"), L("공 색상", "Body color"))),
      Own(new PlanetPartModel(profile.Tail, L("꼬리", "Trail"), L("꼬리 색상", "Trail color"))),
      Own(new PlanetPartModel(profile.Ring, L("고리", "Ring"), L("고리 색상", "Ring color"))),
    };
  }
}
