using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Element.Application.Profiles;
using Element.Configuration.Profiles;
using Element.Presentation.Settings.Window;
using ElementUI;
using UnityEngine;
using static Element.Presentation.Ui.Theme.UiLocale;

namespace Element.Presentation.Settings.Profiles;

/// <summary>
/// General → Profiles: the profile cards, switching, import and a new
/// profile. Each card renames, duplicates, exports and deletes its profile.
/// Messages sit below the cards, so nothing moves under the pointer.
/// </summary>
internal sealed class ProfilesViewModel : SettingsPageViewModel
{
  private readonly IProfiles _profiles;

  // Escape ends a rename; that frame's Escape must not also leave the page.
  private int _renameCancelledFrame = -1;

  // An export waiting for ElementNote to save the key viewers; one at a time.
  private Task<ProfileOutcome> _export;

  // Key viewer swaps that failed before; only a new failure is put into words.
  private int _keyViewerFailuresShown;

  /// <summary>Import and export need the system's file window.</summary>
  public readonly bool FilesSupported;

  /// <summary>A card per profile, in the profiles' order. Replaced whenever the profiles may have changed.</summary>
  public ProfileCardViewModel[] Cards = Array.Empty<ProfileCardViewModel>();

  /// <summary>The heading: how many profiles there are (the window title already says Profiles).</summary>
  public string Count = "";

  /// <summary>"In use: Default", for the link on General.</summary>
  public string InUse = "";

  /// <summary>More than one profile, so one can be deleted.</summary>
  public bool CanDelete;

  /// <summary>What the last action did or why it didn't, under the cards; empty shows nothing.</summary>
  public string Message = "";
  public bool MessageIsProblem;

  /// <summary>The card whose trash was pressed once.</summary>
  public string DeleteArmedId;

  /// <summary>The card whose name is a field, and what is typed in it.</summary>
  public string RenamingId;
  public string RenameText = "";

  public Computed<bool> HasMessage { get; }

  /// <summary>Why the typed name can't be used, or empty.</summary>
  public Computed<string> RenameProblem { get; }

  public Command Import { get; }
  public Command NewProfile { get; }

  public readonly NewProfileViewModel Creating;
  public readonly ProfileFontsViewModel Fonts;

  public ProfilesViewModel(SettingsWindowViewModel window, SettingsPageViewModel parent)
    : base(window, parent, L("프로필", "Profiles"))
  {
    _profiles = window.Features.Profiles;
    FilesSupported = _profiles.FilesSupported;
    HasMessage = Computed(() => Message.Length > 0);
    RenameProblem = Computed(() => RenameProblemOf(_profiles, RenameText, RenamingId, Cards));
    Import = Command(ImportProfile);
    NewProfile = Command(OpenNewProfile);
    Creating = Own(new NewProfileViewModel(_profiles, CreateProfile));
    Fonts = Own(
      new ProfileFontsViewModel(
        Settings,
        window.Features.OverlayFonts,
        () =>
        {
          Window.Changed();
          Window.Save();
        }
      )
    );
    _keyViewerFailuresShown = _profiles.KeyViewerFailures;
    Refresh();
  }

  /// <summary>Opens the page from General's link, fresh.</summary>
  public void Open()
  {
    DeleteArmedId = null;
    RenamingId = null;
    Message = "";
    Refresh();
    Window.Show(this);
  }

  public override void Opened()
  {
    Message = "";
    Creating.Close();
    Refresh();
  }

  public override void Closed()
  {
    Creating.Close();
    Fonts.DismissPicker();
  }

  public override void Tick()
  {
    Fonts.Tick();
    FinishExport();
    ShowKeyViewerFailure();
  }

  public override bool HandleEscape() => RenameTakesEscape() || Fonts.DismissPicker() || Dismiss(Creating);

  public override void SettingsReplaced()
  {
    // A switch replaces the values the font card points at.
    Fonts.Clear();
    Refresh();
  }

  /// <summary>Takes the profiles as they are now. A profile keeps its card.</summary>
  public void Refresh()
  {
    IReadOnlyList<ElementProfile> profiles = _profiles.Profiles;
    ElementProfile active = _profiles.Active;
    ProfileCardViewModel[] previous = Cards;
    var next = new ProfileCardViewModel[profiles.Count];
    for (int i = 0; i < next.Length; i++)
    {
      ElementProfile profile = profiles[i];
      ProfileCardViewModel card = Array.Find(previous, item => item != null && item.Id == profile.Id);
      if (card != null)
        card.Show(profile, _profiles.Summarize(profile), profile == active);
      else
        card = new ProfileCardViewModel(this, profile, _profiles.Summarize(profile), profile == active);
      next[i] = card;
    }
    foreach (ProfileCardViewModel card in previous)
      if (Array.IndexOf(next, card) < 0)
        card.Dispose();
    Cards = next;
    if (RenamingId != null && Find(RenamingId) == null)
      RenamingId = null;
    int count = next.Length;
    Count = L("프로필 " + count + "개", count + (count == 1 ? " profile" : " profiles"));
    InUse = L("사용 중: ", "In use: ") + active?.Name;
    CanDelete = _profiles.CanDelete;
  }

  // ---- Switching and import ----

  public void SwitchProfile(string id)
  {
    DeleteArmedId = null;
    ElementProfile profile = Find(id);
    if (profile == null || profile == _profiles.Active)
      return;
    ProfileOutcome outcome = _profiles.Switch(id);
    Replaced();
    // The card shows the switch; only a problem is put into words.
    ShowMessage(outcome.Ok ? "" : ProfileProblemText.For(outcome, ProfileAction.Switch), true);
    AfterSave(outcome);
  }

  private void ImportProfile()
  {
    DeleteArmedId = null;
    RenamingId = null;
    Creating.Close();
    ProfileOutcome outcome = _profiles.Import(L("가져올 Element 프로필 선택", "Choose an Element profile to import"));
    if (outcome.Problem == ProfileProblem.Cancelled)
      return;
    if (outcome.Ok)
      Replaced();
    else
      Refresh();
    ShowMessage(
      outcome.Ok
        ? L("'" + outcome.Name + "' 프로필을 가져와 바꿨어요.", "Imported '" + outcome.Name + "' and switched to it.")
        : ProfileProblemText.For(outcome, ProfileAction.Import),
      !outcome.Ok
    );
    AfterSave(outcome);
    if (outcome.Ok)
      Fonts.Offer();
  }

  // ---- A new profile, from the panel under its button ----

  private void OpenNewProfile()
  {
    if (Dismiss(Creating))
      return;
    Fonts.DismissPicker();
    DeleteArmedId = null;
    RenamingId = null;
    Creating.Show();
  }

  private static bool Dismiss(NewProfileViewModel creating)
  {
    if (!creating.Open)
      return false;
    creating.Close();
    Runtime.Probe();
    return true;
  }

  // Returns null once the profile is made and in use, or why it wasn't.
  private string CreateProfile(string name)
  {
    ProfileOutcome outcome = _profiles.Create(name);
    if (outcome.Problem == ProfileProblem.Name)
      return ProfileProblemText.Name(outcome.NameProblem);
    if (!outcome.Ok)
      return ProfileProblemText.For(outcome, ProfileAction.Save);
    Replaced();
    ShowMessage(
      L(
        "'" + outcome.Name + "' 프로필을 처음 설정으로 만들었어요. 이제 바꾸는 설정은 이 프로필에 저장됩니다.",
        "Created '" + outcome.Name + "' with the initial settings. Changes you make now are saved to it."
      ),
      false
    );
    AfterSave(outcome);
    return null;
  }

  // ---- Renaming on the card ----

  public void StartRename(string id)
  {
    ElementProfile profile = Find(id);
    if (profile == null)
      return;
    DeleteArmedId = null;
    Message = "";
    RenameText = profile.Name;
    RenamingId = id;
  }

  /// <summary>
  /// The name field was left. Enter or a click elsewhere saves a usable new
  /// name; Escape, the same name or one that can't be used keeps the old name.
  /// </summary>
  public void EndRename(bool cancelled)
  {
    string id = RenamingId;
    if (id == null)
      return;
    RenamingId = null;
    if (cancelled)
    {
      _renameCancelledFrame = Time.frameCount;
      Runtime.Probe();
      return;
    }
    ElementProfile profile = Find(id);
    string name = (RenameText ?? "").Trim();
    if (profile == null || name == profile.Name)
    {
      Runtime.Probe();
      return;
    }
    ProfileOutcome outcome = _profiles.Rename(id, name);
    Refresh();
    if (outcome.Problem == ProfileProblem.Name)
      ShowMessage(
        L("이름을 바꾸지 않았어요. ", "The name wasn't changed. ") + ProfileProblemText.Name(outcome.NameProblem),
        true
      );
    else if (!outcome.Ok)
      ShowMessage(ProfileProblemText.For(outcome, ProfileAction.Save), true);
    AfterSave(outcome);
    Runtime.Probe();
  }

  /// <summary>Escape while renaming ends the rename and stays on the page.</summary>
  private bool RenameTakesEscape()
  {
    if (_renameCancelledFrame == Time.frameCount)
      return true;
    if (RenamingId == null)
      return false;
    RenamingId = null;
    Runtime.Probe();
    return true;
  }

  // ---- Duplicate, delete and export on the card ----

  public void DuplicateProfile(string id)
  {
    ElementProfile profile = Find(id);
    DeleteArmedId = null;
    RenamingId = null;
    if (profile == null)
      return;
    ProfileOutcome outcome = _profiles.Duplicate(id, profile.Name + L(" 복사본", " copy"));
    Refresh();
    ShowMessage(
      outcome.Ok
        ? L("'" + outcome.Name + "' 프로필을 만들었어요.", "Made '" + outcome.Name + "'.")
        : ProfileProblemText.For(outcome, ProfileAction.Save),
      !outcome.Ok
    );
    AfterSave(outcome);
  }

  public void DeleteProfile(string id)
  {
    RenamingId = null;
    if (DeleteArmedId != id)
    {
      DeleteArmedId = id;
      Message = "";
      return;
    }
    DeleteArmedId = null;
    bool wasActive = Find(id) == _profiles.Active;
    ProfileOutcome outcome = _profiles.Delete(id);
    if (outcome.Ok && wasActive)
      Replaced();
    else
      Refresh();
    ShowMessage(
      !outcome.Ok ? ProfileProblemText.For(outcome, ProfileAction.Delete)
        : wasActive
          ? L(
            "'"
              + outcome.Name
              + "' 프로필을 삭제하고 '"
              + _profiles.Active?.Name
              + "' 프로필로 바꿨어요. 삭제 전 설정은 Backups 폴더에 있어요.",
            "Deleted '"
              + outcome.Name
              + "' and switched to '"
              + _profiles.Active?.Name
              + "'. Your settings from before are in the Backups folder."
          )
        : L(
          "'" + outcome.Name + "' 프로필을 삭제했어요. 삭제 전 설정은 Backups 폴더에 있어요.",
          "Deleted '" + outcome.Name + "'. Your settings from before are in the Backups folder."
        ),
      !outcome.Ok
    );
    AfterSave(outcome);
  }

  public void ExportProfile(string id)
  {
    DeleteArmedId = null;
    ElementProfile profile = Find(id);
    if (profile == null)
      return;
    if (_export != null)
      return;
    Task<ProfileOutcome> export = _profiles.Export(
      id,
      L("'" + profile.Name + "' 프로필 내보내기", "Export '" + profile.Name + "'")
    );
    if (export.IsCompleted)
      ShowExport(export);
    else
      _export = export;
  }

  // ElementNote saved the key viewers into the file, or didn't answer in time.
  private void FinishExport()
  {
    if (_export == null || !_export.IsCompleted)
      return;
    Task<ProfileOutcome> export = _export;
    _export = null;
    ShowExport(export);
    ElementUI.Runtime.Probe();
  }

  private void ShowExport(Task<ProfileOutcome> export)
  {
    ProfileOutcome outcome =
      export.Status == TaskStatus.RanToCompletion ? export.Result : ProfileOutcome.Fail(ProfileProblem.WriteFailed);
    if (outcome.Problem == ProfileProblem.Cancelled)
      return;
    if (!outcome.Ok)
    {
      ShowMessage(ProfileProblemText.For(outcome, ProfileAction.Export), true);
      return;
    }
    ShowMessage(
      outcome.KeyViewersLeftOut
        ? L(
          "'"
            + outcome.Detail
            + "' 파일로 내보냈지만 ElementNote가 꺼져 있어 키뷰어는 담지 못했어요. 키뷰어도 담으려면 ElementNote를 켜고 다시 내보내 주세요.",
          "Exported as '"
            + outcome.Detail
            + "', but without key viewers: ElementNote isn't running. Open ElementNote and export again to include them."
        )
        : L("'" + outcome.Detail + "' 파일로 내보냈어요.", "Exported as '" + outcome.Detail + "'."),
      outcome.KeyViewersLeftOut
    );
  }

  // The profile changed, but ElementNote still shows the key viewers of the one before.
  private void ShowKeyViewerFailure()
  {
    int failures = _profiles.KeyViewerFailures;
    if (failures == _keyViewerFailuresShown)
      return;
    _keyViewerFailuresShown = failures;
    string name = _profiles.KeyViewersFailedProfile;
    if (name == null)
      return;
    ShowMessage(
      L(
        "ElementNote 키뷰어를 '"
          + name
          + "' 프로필 것으로 바꾸지 못했어요. ElementNote를 껐다 켜면 다시 바꿔요. 이유는 UMM 로그에 있어요.",
        "ElementNote didn't switch to the key viewers of '"
          + name
          + "'. Restart ElementNote to try again; the UMM log says why."
      ),
      true
    );
    ElementUI.Runtime.Probe();
  }

  // ---- Helpers ----

  private ElementProfile Find(string id)
  {
    foreach (ElementProfile profile in _profiles.Profiles)
      if (profile.Id == id)
        return profile;
    return null;
  }

  // Another profile's values are in place now: every page takes them.
  private void Replaced()
  {
    foreach (SettingsPageViewModel page in Window.Pages)
      page.SettingsReplaced();
  }

  private void AfterSave(ProfileOutcome outcome)
  {
    if (!outcome.Saved)
      Window.Save();
  }

  private void ShowMessage(string text, bool problem)
  {
    Message = text ?? "";
    MessageIsProblem = problem;
  }

  // As the name is typed: the card says why it can't be used. The profile list
  // changes only by this page's actions, which replace the cards.
  [ElementUI.Pure]
  private static string RenameProblemOf(IProfiles profiles, string text, string id, ProfileCardViewModel[] cards)
  {
    if (id == null || text == null || text.Trim().Length == 0)
      return "";
    return profiles.ValidateName(text, id, out ProfileNameProblem problem) ? "" : ProfileProblemText.Name(problem);
  }
}
