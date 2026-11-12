using Element.Application.Profiles;
using Element.Configuration.Profiles;
using ElementUI;
using static Element.Presentation.Ui.Theme.UiLocale;

namespace Element.Presentation.Settings.Pages;

/// <summary>
/// One profile's card: its name (or the field renaming it), what it holds,
/// whether it is in use, and its actions. A click switches to it; delete asks
/// once on the card before it removes.
/// </summary>
internal sealed class ProfileCardModel : ViewModel
{
  public readonly ProfilesModel Page;
  public readonly string Id;
  public string Name = "";

  /// <summary>"17 elements · 3 presets".</summary>
  public string Summary = "";
  public bool InUse;

  /// <summary>The first delete press: the next one deletes.</summary>
  public Computed<bool> Armed { get; }

  /// <summary>The name is a field being edited.</summary>
  public Computed<bool> Renaming { get; }

  /// <summary>Under the name: why a typed name can't be used while renaming, else the summary.</summary>
  public Computed<string> Line { get; }
  public Computed<bool> LineIsProblem { get; }

  /// <summary>Press again to delete, In use, or Click to switch.</summary>
  public Computed<string> State { get; }

  public Command Switch { get; }
  public Command Rename { get; }
  public Command Duplicate { get; }
  public Command Export { get; }
  public Command Delete { get; }

  public ProfileCardModel(ProfilesModel page, ElementProfile profile, ProfileSummary summary, bool inUse)
  {
    Page = page;
    Id = profile.Id;
    Show(profile, summary, inUse);
    Armed = Computed(() => Page.DeleteArmedId == Id);
    Renaming = Computed(() => Page.RenamingId == Id);
    LineIsProblem = Computed(() => Renaming.Value && Page.RenameProblem.Value.Length > 0);
    Line = Computed(() => LineIsProblem.Value ? Page.RenameProblem.Value : Summary);
    State = Computed(() => StateOf(Armed.Value, InUse));
    Switch = Command(() => Page.SwitchProfile(Id));
    Rename = Command(() => Page.StartRename(Id));
    Duplicate = Command(() => Page.DuplicateProfile(Id));
    Export = Command(() => Page.ExportProfile(Id));
    Delete = Command(() => Page.DeleteProfile(Id));
  }

  /// <summary>Takes the profile's name and contents as they are now.</summary>
  public void Show(ElementProfile profile, ProfileSummary summary, bool inUse)
  {
    Name = profile.Name;
    InUse = inUse;
    Summary = L(
      "요소 " + summary.Elements + "개 · 프리셋 " + summary.Presets + "개",
      summary.Elements
        + (summary.Elements == 1 ? " element · " : " elements · ")
        + summary.Presets
        + (summary.Presets == 1 ? " preset" : " presets")
    );
  }

  [ElementUI.Pure]
  private static string StateOf(bool armed, bool inUse) =>
    armed ? L("한 번 더 누르면 삭제해요", "Press again to delete")
    : inUse ? L("사용 중", "In use")
    : L("선택하여 전환", "Click to switch");
}
