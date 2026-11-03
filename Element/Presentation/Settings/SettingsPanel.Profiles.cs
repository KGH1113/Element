using System.Collections.Generic;
using System.Globalization;
using Element.Application.Profiles;
using Element.Configuration.Profiles;
using Element.Presentation.Layout;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Element.Presentation.Settings;

/// <summary>
/// General → Profiles: the profile gallery, switching and import. Each card
/// renames its profile in place, exports it and deletes it; the editor page
/// only names a new profile.
/// </summary>
internal sealed partial class SettingsPanel
{
  private const int ProfilesPage = 9;
  private const int ProfileEditorPage = 10;
  private static readonly Color ErrorText = new(1, .66f, .53f);

  private readonly IProfiles _profiles;
  private SettingsUiFactory _profileGalleryUi;
  private TMP_FontAsset _profilesFont;
  private const float CardActionSize = 32,
    CardActionGap = 4,
    CardActionInset = 12;

  private Button _profileLink,
    _importProfile,
    _newProfile,
    _saveProfile;
  private TextMeshProUGUI _profileLinkDetail,
    _profilesTitle,
    _profilesMessage,
    _profilesHint,
    _profileError,
    _profileIntro;
  private RectTransform _profilesHeading,
    _profileGallery,
    _profileHeader;
  private TMP_InputField _profileName;
  private readonly List<Button> _profileCards = new();

  // The profile whose card shows a name field, and that field.
  private ElementProfile _renaming;
  private TMP_InputField _renameInput;

  // Escape ends a rename; that frame's Escape must not also leave the page.
  private int _renameCancelledFrame = -1;

  // Delete asks twice: the first press arms it and says what will happen.
  private string _deleteArmedId;

  private void CreateProfileLink()
  {
    if (_profiles == null)
      return;
    _profileLink = DetailLink(
      _pages[2].transform,
      L("프로필", "Profiles"),
      "",
      () =>
      {
        _deleteArmedId = null;
        _renaming = null;
        ClearProfileMessages();
        RefreshProfileGallery();
        NavigateTo(ProfilesPage);
      },
      out _profileLinkDetail
    );
    RefreshProfileLink();
  }

  private void ClearProfileMessages()
  {
    if (_profilesMessage == null)
      return;
    _profilesMessage.text = "";
    LayoutProfilePages(_profileGallery.rect.width);
  }

  private void CreateProfilePages(TMP_FontAsset font)
  {
    if (_profiles == null)
      return;
    _profilesFont = font;
    Transform gallery = _pages[ProfilesPage].transform;
    _profilesHeading = _ui.Rect("Profiles heading", gallery, 0, 0, 936, 44);
    _profilesTitle = _ui.Text(L("프로필", "Profiles"), _profilesHeading, 0, 0, 400, 44, 18);
    if (_profiles.FilesSupported)
      _importProfile = _ui.Button(L("가져오기", "Import"), _profilesHeading, 0, 0, 120, 44, ImportProfile);
    _newProfile = _ui.Button(L("새 프로필", "New profile"), _profilesHeading, 0, 0, 156, 44, NewProfile, true);
    _profilesMessage = _ui.Text("", gallery, 0, 56, 936, 24, 14, SettingsUiFactory.Muted);
    _profileGallery = _ui.Rect("Profile gallery", gallery, 0, 96, 936, 160);
    _profilesHint = _ui.Text(
      L(
        "프로필마다 오버레이, 이펙트 프리셋, Tweaks, 하이라이트 색을 따로 저장합니다. 채터 블로커와 일반 설정은 모든 프로필이 함께 씁니다.",
        "Each profile keeps its own overlay, effect presets, tweaks and highlight color. The chatter blocker and General settings are shared."
      ),
      gallery,
      0,
      280,
      936,
      44,
      13,
      SettingsUiFactory.Muted
    );
    _profilesHint.alignment = TextAlignmentOptions.TopLeft;

    Transform editor = _pages[ProfileEditorPage].transform;
    _profileHeader = _ui.Rect("Profile identity", editor, 0, 0, 936, 92);
    _profileName = _ui.Input(
      "Profile name",
      "",
      _profileHeader,
      0,
      0,
      560,
      _ => ValidateProfileName(),
      null,
      ElementProfile.MaximumNameLength
    );
    _profileName.textComponent.textWrappingMode = TextWrappingModes.NoWrap;
    TextMeshProUGUI placeholder = _ui.Text(
      L("프로필 이름", "Profile name"),
      _profileName.textViewport,
      0,
      0,
      300,
      32,
      16,
      SettingsUiFactory.Muted
    );
    SettingsUiFactory.Stretch(placeholder.rectTransform, 0, 0, 0, 0);
    _profileName.placeholder = placeholder;
    placeholder.alignment = _profileName.textComponent.alignment;
    _saveProfile = _ui.Button(L("만들기", "Create"), _profileHeader, 716, 0, 152, 40, CreateProfile, true);
    _profileError = _ui.Text("", _profileHeader, 0, 52, 936, 24, 13, ErrorText);
    _profileIntro = _ui.Text(
      L(
        "지금 쓰는 설정을 복사해 새 프로필을 만들고 바로 사용합니다.",
        "Copies your current settings into a new profile and switches to it."
      ),
      _profileHeader,
      0,
      52,
      936,
      24,
      14,
      SettingsUiFactory.Muted
    );
    RefreshProfileGallery();
  }

  /// <summary>After a switch replaced the live settings: show the new values everywhere.</summary>
  private void RefreshAfterProfileChange()
  {
    _rotationInput?.SetTextWithoutNotify(_settings.Gameplay.Editor.RotationStep.ToString(CultureInfo.InvariantCulture));
    if (_rotationError != null)
      _rotationError.text = "";
    RefreshPresetGallery();
    RefreshProfileGallery();
    _ui.RefreshTheme();
    _galleryUi?.RefreshTheme();
  }

  private void RefreshProfileLink()
  {
    if (_profileLinkDetail != null)
      _profileLinkDetail.text = L("사용 중: ", "In use: ") + _profiles.Active?.Name;
  }

  private void RefreshProfileGallery()
  {
    if (_profileGallery == null)
      return;
    RefreshProfileLink();
    // The old name field goes with its card; it must not save on the way out.
    _renameInput = null;
    for (int i = _profileGallery.childCount - 1; i >= 0; i--)
    {
      GameObject child = _profileGallery.GetChild(i).gameObject;
      child.SetActive(false);
      SettingsUiFactory.Destroy(child);
    }
    _profileGalleryUi?.Dispose();
    _profileGalleryUi = new SettingsUiFactory(_profilesFont) { ReduceMotion = () => _settings.ReduceUiMotion };
    _profileCards.Clear();
    int count = _profiles.Profiles.Count;
    // The window title already says Profiles; the heading counts them.
    _profilesTitle.text = L("프로필 " + count + "개", count + (count == 1 ? " profile" : " profiles"));
    ElementProfile active = _profiles.Active;
    if (_renaming != null && !Contains(_profiles.Profiles, _renaming))
      _renaming = null;
    foreach (ElementProfile item in _profiles.Profiles)
    {
      ElementProfile profile = item;
      bool inUse = profile == active;
      Button card = _profileGalleryUi.Button("", _profileGallery, 0, 0, 300, 140, () => SwitchProfile(profile));
      card.gameObject.name = "Profile: " + profile.Name;
      card.GetComponentInChildren<TextMeshProUGUI>().gameObject.SetActive(false);
      bool renaming = profile == _renaming;
      int actions = 1 + (_profiles.FilesSupported ? 1 : 0) + (_profiles.CanDelete ? 1 : 0);
      float actionsWidth = actions * CardActionSize + (actions - 1) * CardActionGap;
      var title = _profileGalleryUi.Text(profile.Name, card.transform, 18, 18, 220, 54, 20);
      SettingsUiFactory.StretchWidth(title.rectTransform, 18, CardActionInset + actionsWidth + 8);
      title.alignment = TextAlignmentOptions.TopLeft;
      title.gameObject.SetActive(!renaming);
      ProfileSummary summary = _profiles.Summarize(profile);
      var contents = _profileGalleryUi.Text(
        L(
          "요소 " + summary.Elements + "개 · 프리셋 " + summary.Presets + "개",
          summary.Elements
            + (summary.Elements == 1 ? " element · " : " elements · ")
            + summary.Presets
            + (summary.Presets == 1 ? " preset" : " presets")
        ),
        card.transform,
        18,
        82,
        220,
        22,
        13,
        SettingsUiFactory.Muted
      );
      SettingsUiFactory.StretchWidth(contents.rectTransform, 18, 18);
      var state = _profileGalleryUi.Text(
        inUse ? L("사용 중", "In use") : L("선택하여 전환", "Click to switch"),
        card.transform,
        18,
        110,
        240,
        22,
        13,
        inUse ? _ui.SoftAccent : SettingsUiFactory.Muted
      );
      SettingsUiFactory.StretchWidth(state.rectTransform, 18, 18);
      _profileGalleryUi.Bind(() => state.color = inUse ? _ui.SoftAccent : SettingsUiFactory.Muted);
      if (inUse)
      {
        var marker = _profileGalleryUi.Panel("Profile in use", card.transform, 0, 0, 3, 140, _ui.SoftAccent, false);
        marker.rectTransform.anchorMin = new Vector2(0, 0);
        marker.rectTransform.anchorMax = new Vector2(0, 1);
        marker.rectTransform.pivot = new Vector2(0, 1);
        marker.rectTransform.sizeDelta = new Vector2(3, -28);
        marker.rectTransform.anchoredPosition = new Vector2(0, -14);
        _profileGalleryUi.Bind(() => marker.color = _ui.SoftAccent);
      }
      if (renaming)
        _renameInput = CreateRenameField(card.transform, profile);
      else
      {
        int slot = 0;
        if (_profiles.CanDelete)
        {
          bool armed = profile.Id == _deleteArmedId;
          var delete = CardAction(card.transform, "Delete profile", slot++, () => DeleteProfile(profile));
          var icon = delete.gameObject.AddComponent<LayoutActionIcon>();
          icon.Kind = LayoutAction.Delete;
          icon.color = armed ? ErrorText : SettingsUiFactory.Muted;
          icon.raycastTarget = false;
        }
        if (_profiles.FilesSupported)
          AddIcon<SettingsExportIcon>(
            CardAction(card.transform, "Export profile", slot++, () => ExportProfile(profile))
          );
        AddIcon<SettingsEditIcon>(CardAction(card.transform, "Rename profile", slot, () => StartRename(profile)));
      }
      _profileCards.Add(card);
    }
    _ui.RefreshTheme();
    // Rebuilt cards begin at the origin; reflow now, as the preset gallery does.
    LayoutProfilePages(_profileGallery.rect.width);
    if (_renameInput != null)
    {
      _renameInput.Select();
      _renameInput.ActivateInputField();
    }
  }

  // An icon button in the card's top right; slot 0 is the rightmost.
  private RectTransform CardAction(Transform card, string name, int slot, System.Action action)
  {
    Button button = _profileGalleryUi.Button("", card, 0, 0, CardActionSize, CardActionSize, action);
    button.gameObject.name = name;
    button.GetComponentInChildren<TextMeshProUGUI>().gameObject.SetActive(false);
    RectTransform rect = (RectTransform)button.transform;
    rect.anchorMin = rect.anchorMax = new Vector2(1, 1);
    rect.pivot = new Vector2(1, 1);
    rect.anchoredPosition = new Vector2(-CardActionInset - slot * (CardActionSize + CardActionGap), -CardActionInset);
    return _profileGalleryUi.Rect(name + " icon", rect, 7, 7, 18, 18);
  }

  private static void AddIcon<T>(RectTransform slot)
    where T : SettingsVectorGraphic
  {
    T icon = slot.gameObject.AddComponent<T>();
    icon.color = SettingsUiFactory.Muted;
    icon.raycastTarget = false;
  }

  private static bool Contains(IReadOnlyList<ElementProfile> profiles, ElementProfile profile)
  {
    foreach (ElementProfile item in profiles)
      if (item == profile)
        return true;
    return false;
  }

  private void SwitchProfile(ElementProfile profile)
  {
    bool disarmed = _deleteArmedId != null;
    _deleteArmedId = null;
    if (profile == _profiles.Active)
    {
      if (disarmed)
      {
        RefreshProfileGallery();
        ShowProfilesMessage("", false);
      }
      return;
    }
    ProfileOutcome outcome = _profiles.Switch(profile.Id);
    RefreshAfterProfileChange();
    ShowProfilesMessage(
      outcome.Ok
        ? L("'" + outcome.Name + "' 프로필로 바꿨어요.", "Switched to '" + outcome.Name + "'.")
        : ProfileProblemText(outcome, ProfileAction.Switch),
      !outcome.Ok
    );
    AfterProfileSave(outcome);
  }

  private void ImportProfile()
  {
    bool reset = _deleteArmedId != null || _renaming != null;
    _deleteArmedId = null;
    _renaming = null;
    ProfileOutcome outcome = _profiles.Import(L("가져올 Element 프로필 선택", "Choose an Element profile to import"));
    if (outcome.Problem == ProfileProblem.Cancelled)
    {
      if (reset)
      {
        RefreshProfileGallery();
        ShowProfilesMessage("", false);
      }
      return;
    }
    if (outcome.Ok)
      RefreshAfterProfileChange();
    else
      RefreshProfileGallery();
    ShowProfilesMessage(
      outcome.Ok
        ? L("'" + outcome.Name + "' 프로필을 가져와 바꿨어요.", "Imported '" + outcome.Name + "' and switched to it.")
        : ProfileProblemText(outcome, ProfileAction.Import),
      !outcome.Ok
    );
    AfterProfileSave(outcome);
  }

  // ---- Renaming on the card ----

  private TMP_InputField CreateRenameField(Transform card, ElementProfile profile)
  {
    TMP_InputField input = null;
    input = _profileGalleryUi.Input(
      "Profile name",
      profile.Name,
      card,
      12,
      12,
      200,
      text => ShowRenameProblem(profile, text),
      text => CommitRename(input, profile, text),
      ElementProfile.MaximumNameLength
    );
    input.textComponent.textWrappingMode = TextWrappingModes.NoWrap;
    SettingsUiFactory.StretchWidth((RectTransform)input.transform, 12, 12);
    return input;
  }

  private void StartRename(ElementProfile profile)
  {
    _deleteArmedId = null;
    _renaming = profile;
    _profilesMessage.text = "";
    RefreshProfileGallery();
  }

  // As the name is typed: a name that can't be used says why at once.
  private void ShowRenameProblem(ElementProfile profile, string text)
  {
    bool valid = _profiles.ValidateName(text, profile.Id, out ProfileNameProblem problem);
    ShowProfilesMessage(valid || text.Trim().Length == 0 ? "" : ProfileNameText(problem), true);
  }

  // Enter or a click elsewhere saves a usable new name; Escape, the same
  // name or one that can't be used puts the old name back.
  private void CommitRename(TMP_InputField input, ElementProfile profile, string text)
  {
    if (input == null || input != _renameInput)
      return;
    bool cancelled = input.wasCanceled;
    if (cancelled)
      _renameCancelledFrame = Time.frameCount;
    _renaming = null;
    _renameInput = null;
    string name = (text ?? "").Trim();
    if (cancelled || name == profile.Name)
    {
      RefreshProfileGallery();
      ShowProfilesMessage("", false);
      return;
    }
    ProfileOutcome outcome = _profiles.Rename(profile.Id, name);
    RefreshProfileGallery();
    if (outcome.Problem == ProfileProblem.Name)
      ShowProfilesMessage(
        L("이름을 바꾸지 않았어요. ", "The name wasn't changed. ") + ProfileNameText(outcome.NameProblem),
        true
      );
    else if (!outcome.Ok)
      ShowProfilesMessage(ProfileProblemText(outcome, ProfileAction.Save), true);
    else
      ShowProfilesMessage("", false);
    AfterProfileSave(outcome);
  }

  /// <summary>Escape while renaming ends the rename and stays on the page.</summary>
  private bool ProfileRenameTakesEscape()
  {
    if (_renameCancelledFrame == Time.frameCount)
      return true;
    if (_renaming == null)
      return false;
    _renaming = null;
    _renameInput = null;
    RefreshProfileGallery();
    ShowProfilesMessage("", false);
    return true;
  }

  // ---- A new profile ----

  private void NewProfile()
  {
    _deleteArmedId = null;
    _renaming = null;
    _profileName.SetTextWithoutNotify("");
    _profileError.text = "";
    _profileIntro.gameObject.SetActive(true);
    _saveProfile.interactable = false;
    _ui.RefreshTheme();
    NavigateTo(ProfileEditorPage);
    _profileName.Select();
    _profileName.ActivateInputField();
  }

  private void ValidateProfileName()
  {
    if (_saveProfile == null)
      return;
    bool valid = _profiles.ValidateName(_profileName.text, null, out ProfileNameProblem problem);
    _saveProfile.interactable = valid;
    _profileError.text = valid || _profileName.text.Length == 0 ? "" : ProfileNameText(problem);
    _profileIntro.gameObject.SetActive(_profileError.text.Length == 0);
  }

  private void CreateProfile()
  {
    ProfileOutcome outcome = _profiles.Create(_profileName.text);
    if (outcome.Problem == ProfileProblem.Name)
    {
      ShowProfileError(ProfileNameText(outcome.NameProblem));
      _profileName.Select();
      return;
    }
    if (!outcome.Ok)
    {
      ShowProfileError(ProfileProblemText(outcome, ProfileAction.Save));
      return;
    }
    RefreshProfileGallery();
    SelectPage(ProfilesPage);
    ShowProfilesMessage(
      L(
        "'" + outcome.Name + "' 프로필을 만들었어요. 이제 바꾸는 설정은 이 프로필에 저장됩니다.",
        "Created '" + outcome.Name + "'. Changes you make now are saved to it."
      ),
      false
    );
    AfterProfileSave(outcome);
  }

  // ---- Delete and export on the card ----

  private void DeleteProfile(ElementProfile profile)
  {
    _renaming = null;
    if (_deleteArmedId != profile.Id)
    {
      _deleteArmedId = profile.Id;
      RefreshProfileGallery();
      ShowProfilesMessage(
        L(
          "한 번 더 누르면 '" + profile.Name + "' 프로필을 삭제합니다. 삭제 전 설정은 Backups 폴더에 보관돼요.",
          "Press again to delete '" + profile.Name + "'. Your settings are backed up to the Backups folder first."
        ),
        true
      );
      return;
    }
    _deleteArmedId = null;
    bool wasActive = profile == _profiles.Active;
    ProfileOutcome outcome = _profiles.Delete(profile.Id);
    if (outcome.Ok && wasActive)
      RefreshAfterProfileChange();
    else
      RefreshProfileGallery();
    ShowProfilesMessage(
      !outcome.Ok ? ProfileProblemText(outcome, ProfileAction.Delete)
        : wasActive
          ? L(
            "'" + outcome.Name + "' 프로필을 삭제하고 '" + _profiles.Active?.Name + "' 프로필로 바꿨어요.",
            "Deleted '" + outcome.Name + "' and switched to '" + _profiles.Active?.Name + "'."
          )
        : L("'" + outcome.Name + "' 프로필을 삭제했어요.", "Deleted '" + outcome.Name + "'."),
      !outcome.Ok
    );
    AfterProfileSave(outcome);
  }

  private void ExportProfile(ElementProfile profile)
  {
    bool disarmed = _deleteArmedId != null;
    _deleteArmedId = null;
    if (disarmed)
      RefreshProfileGallery();
    ProfileOutcome outcome = _profiles.Export(
      profile.Id,
      L("'" + profile.Name + "' 프로필 내보내기", "Export '" + profile.Name + "'")
    );
    if (outcome.Problem == ProfileProblem.Cancelled)
    {
      if (disarmed)
        ShowProfilesMessage("", false);
      return;
    }
    ShowProfilesMessage(
      outcome.Ok
        ? L("'" + outcome.Detail + "' 파일로 내보냈어요.", "Exported as '" + outcome.Detail + "'.")
        : ProfileProblemText(outcome, ProfileAction.Export),
      !outcome.Ok
    );
  }

  private void AfterProfileSave(ProfileOutcome outcome)
  {
    if (!outcome.Saved)
      ShowSaveError();
  }

  private void ShowProfilesMessage(string text, bool error)
  {
    _profilesMessage.text = text;
    _profilesMessage.color = error ? ErrorText : _ui.SoftAccent;
    LayoutProfilePages(_profileGallery.rect.width);
  }

  private void ShowProfileError(string text)
  {
    _profileError.text = text;
    _profileIntro.gameObject.SetActive(text.Length == 0);
    LayoutProfilePages(_profileGallery.rect.width);
  }

  private static void SetText(Button button, string text)
  {
    TextMeshProUGUI label = button.GetComponentInChildren<TextMeshProUGUI>(true);
    if (label != null)
      label.text = text;
  }

  private enum ProfileAction
  {
    Switch,
    Import,
    Export,
    Save,
    Delete,
  }

  private static string ProfileNameText(ProfileNameProblem problem) =>
    problem switch
    {
      ProfileNameProblem.Empty => L("프로필 이름을 입력하세요.", "Give the profile a name."),
      ProfileNameProblem.TooLong => L(
        "이름은 " + ElementProfile.MaximumNameLength + "자 이내로 입력하세요.",
        "Use " + ElementProfile.MaximumNameLength + " characters or fewer."
      ),
      _ => L(
        "같은 이름의 프로필이 있어요. 다른 이름을 입력하세요.",
        "A profile already has this name. Choose another."
      ),
    };

  // What happened and what to do next, in the player's terms.
  private static string ProfileProblemText(ProfileOutcome outcome, ProfileAction action) =>
    outcome.Problem switch
    {
      ProfileProblem.NotProfile => L(
        "Element 프로필 파일이 아니에요. 확장자가 ." + ProfileFile.Extension + "인 파일을 골라 주세요.",
        "That isn't an Element profile. Choose a ." + ProfileFile.Extension + " file."
      ),
      ProfileProblem.NewerVersion => string.IsNullOrEmpty(outcome.Detail)
        ? L(
          "더 새로운 Element에서 만든 프로필이에요. Element를 업데이트한 뒤 다시 가져와 주세요.",
          "This profile was made with a newer Element. Update Element, then import it again."
        )
        : L(
          "Element " + outcome.Detail + "에서 만든 프로필이에요. Element를 업데이트한 뒤 다시 가져와 주세요.",
          "This profile was made with Element " + outcome.Detail + ". Update Element, then import it again."
        ),
      ProfileProblem.TooLarge => L(
        "파일이 너무 커서 프로필로 읽지 않았어요. 고른 파일이 프로필이 맞는지 확인해 주세요.",
        "That file is too large to be a profile. Check that you chose the right file."
      ),
      ProfileProblem.ReadFailed => L(
        "파일을 열지 못했어요. 파일이 아직 내려받는 중이거나 다른 프로그램이 쓰고 있지 않은지 확인해 주세요.",
        "Couldn't open the file. Make sure it has finished downloading and no other program is using it."
      ),
      ProfileProblem.WriteFailed => L(
        "파일을 저장하지 못했어요. 다른 폴더를 골라 다시 내보내 주세요.",
        "Couldn't save the file. Choose another folder and export again."
      ),
      ProfileProblem.DialogUnavailable => L(
        "파일 창을 열지 못했어요. Element를 다시 설치한 뒤 시도해 주세요. 이유는 UMM 로그에 있어요.",
        "Couldn't open the file window. Reinstall Element and try again; the UMM log says why."
      ),
      ProfileProblem.LastProfile => L(
        "프로필이 하나뿐이라 삭제할 수 없어요. 새 프로필을 만든 뒤 삭제해 주세요.",
        "This is your only profile, so it can't be deleted. Create another one first."
      ),
      ProfileProblem.Missing => L("이 프로필은 이미 삭제됐어요.", "This profile no longer exists."),
      ProfileProblem.Damaged when action == ProfileAction.Import => L(
        "프로필 파일이 손상되어 가져오지 못했어요. 파일을 다시 받아 가져와 주세요.",
        "The profile file is damaged, so it wasn't imported. Get the file again and import it."
      ),
      _ => L(
        "프로필을 불러오지 못해 지금 설정을 그대로 두었어요. 이유는 UMM 로그에 있어요.",
        "Couldn't load that profile, so your current settings stay. The UMM log says why."
      ),
    };

  private void LayoutProfilePages(float width)
  {
    if (_profilesHeading == null)
      return;
    bool stackedButtons = width < 440;
    float buttonsTop = stackedButtons ? 52 : 0;
    float headingHeight = stackedButtons ? 96 : 44;
    SettingsUiFactory.Place(_profilesHeading, 0, 0, width, headingHeight);
    SettingsUiFactory.Place(_profilesTitle.rectTransform, 0, 0, stackedButtons ? width : width - 300, 44);
    float newWidth = stackedButtons && _importProfile != null ? (width - 8) / 2 : 156;
    float importWidth = stackedButtons ? newWidth : 120;
    SettingsUiFactory.Place((RectTransform)_newProfile.transform, width - newWidth, buttonsTop, newWidth, 44);
    if (_importProfile != null)
      SettingsUiFactory.Place(
        (RectTransform)_importProfile.transform,
        width - newWidth - 8 - importWidth,
        buttonsTop,
        importWidth,
        44
      );
    float top = headingHeight + 12;
    bool hasMessage = !string.IsNullOrEmpty(_profilesMessage.text);
    _profilesMessage.gameObject.SetActive(hasMessage);
    if (hasMessage)
    {
      float messageHeight = TextHeight(_profilesMessage, width, 20);
      SettingsUiFactory.Place(_profilesMessage.rectTransform, 0, top, width, messageHeight);
      top += messageHeight + 12;
    }
    int columns =
      width >= 600 ? 3
      : width >= 440 ? 2
      : 1;
    float cardWidth = (width - (columns - 1) * 12) / columns;
    float galleryHeight = Mathf.Max(140, Mathf.CeilToInt(_profileCards.Count / (float)columns) * 152 - 12);
    SettingsUiFactory.Place(_profileGallery, 0, top, width, galleryHeight);
    for (int i = 0; i < _profileCards.Count; i++)
      SettingsUiFactory.Place(
        (RectTransform)_profileCards[i].transform,
        i % columns * (cardWidth + 12),
        i / columns * 152,
        cardWidth,
        140
      );
    float hintTop = top + galleryHeight + 16;
    float hintHeight = TextHeight(_profilesHint, width, 20);
    SettingsUiFactory.Place(_profilesHint.rectTransform, 0, hintTop, width, hintHeight);
    ((RectTransform)_pages[ProfilesPage].transform).sizeDelta = new Vector2(width, hintTop + hintHeight);
    LayoutProfileEditor(width);
  }

  private void LayoutProfileEditor(float width)
  {
    if (_profileHeader == null)
      return;
    bool narrow = width < 600;
    float messageTop = narrow ? 104 : 52;
    TextMeshProUGUI message = _profileError.text.Length > 0 ? _profileError : _profileIntro;
    float messageHeight = TextHeight(message, width, 24);
    float identityHeight = messageTop + messageHeight;
    SettingsUiFactory.Place(_profileHeader, 0, 0, width, identityHeight);
    SettingsUiFactory.Place((RectTransform)_profileName.transform, 0, 0, narrow ? width : width - 168, 40);
    SettingsUiFactory.Place((RectTransform)_saveProfile.transform, width - 152, narrow ? 52 : 0, 152, 40);
    SettingsUiFactory.Place(_profileError.rectTransform, 0, messageTop, width, messageHeight);
    SettingsUiFactory.Place(_profileIntro.rectTransform, 0, messageTop, width, messageHeight);
    ((RectTransform)_pages[ProfileEditorPage].transform).sizeDelta = new Vector2(width, identityHeight);
  }

  // Rounded up: Place rounds sizes, and a box a fraction short of the last
  // line would end the text with an ellipsis.
  private static float TextHeight(TextMeshProUGUI text, float width, float minimum) =>
    Mathf.Max(minimum, Mathf.Ceil(text.GetPreferredValues(width, 0).y) + 1);

  /// <summary>The profile link at the top of General; returns where the rest of General starts.</summary>
  private float LayoutProfileLink(float width)
  {
    if (_profileLink == null)
      return 0;
    SettingsUiFactory.Place((RectTransform)_profileLink.transform, 0, 0, width, 80);
    return 92;
  }
}
