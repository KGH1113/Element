using ElementUI;
using static ElementUI.UI;

namespace Element.Presentation.Settings.Pages;

internal sealed class ProfilesView : View<ProfilesModel>
{
  public ProfilesView(ProfilesModel vm)
    : base(vm) { }

  public override View Body => Text("Profiles");
}
