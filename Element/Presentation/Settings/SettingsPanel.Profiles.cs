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
/// General → Profiles: the profile gallery, switching, import and a new
/// profile. Each card renames, duplicates, exports and deletes its profile.
/// Messages sit below the cards, so nothing moves under the pointer.
/// </summary>
internal sealed partial class SettingsPanel
{
  private const int ProfilesPage = 9;
  private static readonly Color ErrorText = new(1, .66f, .53f);

  private const float CardActionSize = 30,
    CardActionGap = 2,
    CardActionInset = 10,
    CardGlyphSize = 18,
    NewProfileWidth = 340,
    NewProfilePadding = 14;

  private readonly IProfiles _profiles;
  private SettingsUiFactory _profileGalleryUi;
  private TMP_FontAsset _profilesFont;
  private Button _profileLink,
    _importProfile,
    _newProfile,
    _createProfile;
  private TextMeshProUGUI _profileLinkDetail,
    _profilesTitle,
    _profilesMessage,
    _newProfileHint,
    _newProfileError;
  private RectTransform _profilesHeading,
    _profileGallery,
    _newProfilePopover;
  private TMP_InputField _newProfileName;
  private readonly List<Button> _profileCards = new();

  // The profile whose card shows a name field, that field, and the card line
  // that says why a typed name can't be used.
  private ElementProfile _renaming;
  private TMP_InputField _renameInput;
  private TextMeshProUGUI _renameProblem;
  private string _renameSummary;

  // Escape ends a rename; that frame's Escape must not also leave the page.
  private int _renameCancelledFrame = -1;

  // Delete asks twice: the first press turns the card's trash red.
  private string _deleteArmedId;

  // TMP places the caret of a field focused before its text is laid out at
  // nothing, and keeps it there until typing; the next frame places it again.
  private TMP_InputField _focusField;
  private int _focusFrame;

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
        _profilesMessage.text = "";
        RefreshProfileGallery();
        NavigateTo(ProfilesPage);
      },
      out _profileLinkDetail
    );
    RefreshProfileLink();
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
    _newProfile = _ui.Button(L("새 프로필", "New profile"), _profilesHeading, 0, 0, 156, 44, ToggleNewProfile, true);
    _profileGallery = _ui.Rect("Profile gallery", gallery, 0, 56, 936, 160);
    _profilesMessage = _ui.Text("", gallery, 0, 0, 936, 24, 14, SettingsUiFactory.Muted);
    _profilesMessage.alignment = TextAlignmentOptions.TopLeft;
    CreateFontReplacementCard(gallery);
    CreateNewProfilePopover();
    RefreshProfileGallery();
  }

  // A small panel under New profile: a name, and Create.
  private void CreateNewProfilePopover()
  {
    Image panel = _ui.Panel("New profile", _window, 0, 0, NewProfileWidth, 120, new Color(.105f, .105f, .105f));
    panel.raycastTarget = true;
    _newProfilePopover = panel.rectTransform;
    float inner = NewProfileWidth - NewProfilePadding * 2;
    _newProfileHint = _ui.Text(
      L("처음 설정으로 시작해요.", "Starts with the initial settings."),
      _newProfilePopover,
      NewProfilePadding,
      NewProfilePadding,
      inner,
      20,
      13,
      SettingsUiFactory.Muted
    );
    _newProfileName = _ui.Input(
      "Profile name",
      "",
      _newProfilePopover,
      NewProfilePadding,
      42,
      inner - 96,
      _ => ValidateNewProfileName(),
      null,
      ElementProfile.MaximumNameLength
    );
    _newProfileName.textComponent.textWrappingMode = TextWrappingModes.NoWrap;
    TextMeshProUGUI placeholder = _ui.Text(
      L("프로필 이름", "Profile name"),
      _newProfileName.textViewport,
      0,
      0,
      300,
      32,
      16,
      SettingsUiFactory.Muted
    );
    SettingsUiFactory.Stretch(placeholder.rectTransform, 0, 0, 0, 0);
    _newProfileName.placeholder = placeholder;
    placeholder.alignment = _newProfileName.textComponent.alignment;
    _newProfileName.onSubmit.AddListener(_ => CreateProfile());
    _createProfile = _ui.Button(
      L("만들기", "Create"),
      _newProfilePopover,
      NewProfileWidth - NewProfilePadding - 88,
      42,
      88,
      40,
      CreateProfile,
      true
    );
    _newProfileError = _ui.Text("", _newProfilePopover, NewProfilePadding, 90, inner, 20, 13, ErrorText);
    _newProfileError.alignment = TextAlignmentOptions.TopLeft;
    _newProfilePopover.gameObject.SetActive(false);
  }

  /// <summary>After a switch replaced the live settings: show the new values everywhere.</summary>
  private void RefreshAfterProfileChange()
  {
    // A switch replaces the values the font card points at.
    ClearFontReplacements();
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
    _renameProblem = null;
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
      bool renaming = profile == _renaming;
      bool armed = profile.Id == _deleteArmedId;
      Button card = _profileGalleryUi.Button("", _profileGallery, 0, 0, 300, 140, () => SwitchProfile(profile));
      card.gameObject.name = "Profile: " + profile.Name;
      card.GetComponentInChildren<TextMeshProUGUI>().gameObject.SetActive(false);
      int actions = 2 + (_profiles.FilesSupported ? 1 : 0) + (_profiles.CanDelete ? 1 : 0);
      float actionsWidth = actions * CardActionSize + (actions - 1) * CardActionGap;
      var title = _profileGalleryUi.Text(profile.Name, card.transform, 18, 18, 220, 54, 20);
      SettingsUiFactory.StretchWidth(title.rectTransform, 18, CardActionInset + actionsWidth + 8);
      title.alignment = TextAlignmentOptions.TopLeft;
      title.gameObject.SetActive(!renaming);
      ProfileSummary summary = _profiles.Summarize(profile);
      string summaryText = L(
        "요소 " + summary.Elements + "개 · 프리셋 " + summary.Presets + "개",
        summary.Elements
          + (summary.Elements == 1 ? " element · " : " elements · ")
          + summary.Presets
          + (summary.Presets == 1 ? " preset" : " presets")
      );
      var contents = _profileGalleryUi.Text(summaryText, card.transform, 18, 82, 220, 22, 13, SettingsUiFactory.Muted);
      SettingsUiFactory.StretchWidth(contents.rectTransform, 18, 18);
      var state = _profileGalleryUi.Text(
        armed ? L("한 번 더 누르면 삭제해요", "Press again to delete")
          : inUse ? L("사용 중", "In use")
          : L("선택하여 전환", "Click to switch"),
        card.transform,
        18,
        110,
        240,
        22,
        13
      );
      SettingsUiFactory.StretchWidth(state.rectTransform, 18, 18);
      _profileGalleryUi.Bind(() =>
        state.color =
          armed ? ErrorText
          : inUse ? _ui.SoftAccent
          : SettingsUiFactory.Muted
      );
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
      {
        _renameInput = CreateRenameField(card.transform, profile);
        _renameProblem = contents;
        _renameSummary = summaryText;
      }
      else
      {
        int slot = 0;
        if (_profiles.CanDelete)
          CardAction<LayoutActionIcon>(
            card.transform,
            "Delete profile",
            slot++,
            () => DeleteProfile(profile),
            armed ? ErrorText : SettingsUiFactory.Muted,
            armed ? ErrorText : SettingsUiFactory.Foreground
          ).Kind = LayoutAction.Delete;
        if (_profiles.FilesSupported)
          CardAction<SettingsExportIcon>(card.transform, "Export profile", slot++, () => ExportProfile(profile));
        CardAction<SettingsDuplicateIcon>(card.transform, "Duplicate profile", slot++, () => DuplicateProfile(profile));
        CardAction<SettingsEditIcon>(card.transform, "Rename profile", slot, () => StartRename(profile));
      }
      _profileCards.Add(card);
    }
    _ui.RefreshTheme();
    // Rebuilt cards begin at the origin; reflow now, as the preset gallery does.
    LayoutProfilePages(_profileGallery.rect.width);
    if (_renameInput != null)
      Focus(_renameInput);
  }

  // A glyph button in the card's top right; slot 0 is the rightmost.
  private T CardAction<T>(
    Transform card,
    string name,
    int slot,
    System.Action action,
    Color? normal = null,
    Color? hover = null
  )
    where T : Graphic
  {
    Button button = _profileGalleryUi.GlyphButton(
      name,
      card,
      CardActionSize,
      CardGlyphSize,
      action,
      normal ?? SettingsUiFactory.Muted,
      hover ?? SettingsUiFactory.Foreground,
      out T glyph
    );
    RectTransform rect = (RectTransform)button.transform;
    rect.anchorMin = rect.anchorMax = new Vector2(1, 1);
    rect.pivot = new Vector2(1, 1);
    rect.anchoredPosition = new Vector2(-CardActionInset - slot * (CardActionSize + CardActionGap), -CardActionInset);
    return glyph;
  }

  private static bool Contains(IReadOnlyList<ElementProfile> profiles, ElementProfile profile)
  {
    foreach (ElementProfile item in profiles)
      if (item == profile)
        return true;
    return false;
  }

  /// <summary>Focuses a field now and places its caret again next frame.</summary>
  private void Focus(TMP_InputField field)
  {
    field.Select();
    field.ActivateInputField();
    _focusField = field;
    _focusFrame = Time.frameCount;
  }

  private void SettleFocus()
  {
    if (_focusField is null || Time.frameCount <= _focusFrame)
      return;
    TMP_InputField field = _focusField;
    _focusField = null;
    if (field == null || !field.isFocused)
      return;
    field.textComponent.ForceMeshUpdate();
    field.MoveTextEnd(false);
  }

  private void SwitchProfile(ElementProfile profile)
  {
    bool disarmed = _deleteArmedId != null;
    _deleteArmedId = null;
    if (profile == _profiles.Active)
    {
      if (disarmed)
        RefreshProfileGallery();
      return;
    }
    ProfileOutcome outcome = _profiles.Switch(profile.Id);
    RefreshAfterProfileChange();
    // The card shows the switch; only a problem is put into words.
    ShowProfilesMessage(outcome.Ok ? "" : ProfileProblemText(outcome, ProfileAction.Switch), true);
    AfterProfileSave(outcome);
  }

  private void ImportProfile()
  {
    bool reset = _deleteArmedId != null || _renaming != null;
    _deleteArmedId = null;
    _renaming = null;
    HideNewProfile();
    ProfileOutcome outcome = _profiles.Import(L("가져올 Element 프로필 선택", "Choose an Element profile to import"));
    if (outcome.Problem == ProfileProblem.Cancelled)
    {
      if (reset)
        RefreshProfileGallery();
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
    if (outcome.Ok)
      OfferFontReplacements();
  }

  // ---- A new profile, from the popover under its button ----

  private void ToggleNewProfile()
  {
    if (DismissNewProfile())
      return;
    if (!CommitPopover())
      return;
    HidePopover();
    _deleteArmedId = null;
    _renaming = null;
    RefreshProfileGallery();
    _newProfileName.SetTextWithoutNotify("");
    _newProfileError.text = "";
    _createProfile.interactable = false;
    _pickerShade.gameObject.SetActive(true);
    _pickerShade.transform.SetAsLastSibling();
    _newProfilePopover.gameObject.SetActive(true);
    _ui.RefreshTheme();
    LayoutNewProfilePopover();
    Focus(_newProfileName);
  }

  private void HideNewProfile()
  {
    if (_newProfilePopover == null || !_newProfilePopover.gameObject.activeSelf)
      return;
    _newProfileName.DeactivateInputField();
    _newProfilePopover.gameObject.SetActive(false);
    if (_planetPicker?.Visible != true && _fontPopover?.Visible != true)
      _pickerShade.gameObject.SetActive(false);
  }

  private bool DismissNewProfile()
  {
    if (_newProfilePopover == null || !_newProfilePopover.gameObject.activeSelf)
      return false;
    HideNewProfile();
    return true;
  }

  private void ValidateNewProfileName()
  {
    bool valid = _profiles.ValidateName(_newProfileName.text, null, out ProfileNameProblem problem);
    _createProfile.interactable = valid;
    _newProfileError.text = valid || _newProfileName.text.Trim().Length == 0 ? "" : ProfileNameText(problem);
    LayoutNewProfilePopover();
  }

  private void CreateProfile()
  {
    if (_newProfilePopover == null || !_newProfilePopover.gameObject.activeSelf)
      return;
    ProfileOutcome outcome = _profiles.Create(_newProfileName.text);
    if (outcome.Problem == ProfileProblem.Name)
    {
      _newProfileError.text = ProfileNameText(outcome.NameProblem);
      LayoutNewProfilePopover();
      Focus(_newProfileName);
      return;
    }
    if (!outcome.Ok)
    {
      _newProfileError.text = ProfileProblemText(outcome, ProfileAction.Save);
      LayoutNewProfilePopover();
      return;
    }
    HideNewProfile();
    RefreshAfterProfileChange();
    ShowProfilesMessage(
      L(
        "'" + outcome.Name + "' 프로필을 처음 설정으로 만들었어요. 이제 바꾸는 설정은 이 프로필에 저장됩니다.",
        "Created '" + outcome.Name + "' with the initial settings. Changes you make now are saved to it."
      ),
      false
    );
    AfterProfileSave(outcome);
  }

  // Under New profile, its right edge on the button's, kept inside the window.
  private void LayoutNewProfilePopover()
  {
    if (_newProfilePopover == null || !_newProfilePopover.gameObject.activeSelf)
      return;
    SettingsUiFactory.Place((RectTransform)_pickerShade.transform, 0, 0, _window.rect.width, _window.rect.height);
    float width = Mathf.Min(NewProfileWidth, _window.rect.width - 20);
    float inner = width - NewProfilePadding * 2;
    float hintHeight = TextHeight(_newProfileHint, inner, 20);
    SettingsUiFactory.Place(_newProfileHint.rectTransform, NewProfilePadding, NewProfilePadding, inner, hintHeight);
    float y = NewProfilePadding + hintHeight + 8;
    SettingsUiFactory.Place((RectTransform)_newProfileName.transform, NewProfilePadding, y, inner - 96, 40);
    SettingsUiFactory.Place((RectTransform)_createProfile.transform, width - NewProfilePadding - 88, y, 88, 40);
    y += 40;
    bool hasError = _newProfileError.text.Length > 0;
    _newProfileError.gameObject.SetActive(hasError);
    if (hasError)
    {
      float errorHeight = TextHeight(_newProfileError, inner, 20);
      SettingsUiFactory.Place(_newProfileError.rectTransform, NewProfilePadding, y + 8, inner, errorHeight);
      y += 8 + errorHeight;
    }
    float height = y + NewProfilePadding;
    RectTransform anchor = (RectTransform)_newProfile.transform;
    Vector3 local = _window.InverseTransformPoint(anchor.position);
    float x = Mathf.Clamp(local.x - _window.rect.xMin + anchor.rect.width - width, 10, _window.rect.width - width - 10);
    float top = Mathf.Clamp(
      _window.rect.yMax - local.y + anchor.rect.height + 8,
      10,
      _window.rect.height - height - 10
    );
    SettingsUiFactory.Place(_newProfilePopover, x, top, width, height);
    _newProfilePopover.SetAsLastSibling();
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
    // The caret goes after the name, where typing continues.
    input.onFocusSelectAll = false;
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

  // As the name is typed: the card says why a name can't be used, in place of its summary.
  private void ShowRenameProblem(ElementProfile profile, string text)
  {
    if (_renameProblem == null)
      return;
    bool valid = _profiles.ValidateName(text, profile.Id, out ProfileNameProblem problem);
    bool shown = !valid && text.Trim().Length > 0;
    _renameProblem.text = shown ? ProfileNameText(problem) : _renameSummary;
    _renameProblem.color = shown ? ErrorText : SettingsUiFactory.Muted;
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
    return true;
  }

  // ---- Duplicate, delete and export on the card ----

  private void DuplicateProfile(ElementProfile profile)
  {
    _deleteArmedId = null;
    _renaming = null;
    ProfileOutcome outcome = _profiles.Duplicate(profile.Id, profile.Name + L(" 복사본", " copy"));
    RefreshProfileGallery();
    ShowProfilesMessage(
      outcome.Ok
        ? L("'" + outcome.Name + "' 프로필을 만들었어요.", "Made '" + outcome.Name + "'.")
        : ProfileProblemText(outcome, ProfileAction.Save),
      !outcome.Ok
    );
    AfterProfileSave(outcome);
  }

  private void DeleteProfile(ElementProfile profile)
  {
    _renaming = null;
    if (_deleteArmedId != profile.Id)
    {
      _deleteArmedId = profile.Id;
      _profilesMessage.text = "";
      RefreshProfileGallery();
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
      return;
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

  private void ClearProfileMessages()
  {
    if (_profilesMessage == null)
      return;
    _profilesMessage.text = "";
    LayoutProfilePages(_profileGallery.rect.width);
  }

  private static void SetText(Button button, string text)
  {
    TextMeshProUGUI label = button.GetComponentInChildren<TextMeshProUGUI>(true);
    if (label != null)
      label.text = text;
  }

  // Below the cards: a message appearing or going never moves them.
  private void ShowProfilesMessage(string text, bool error)
  {
    _profilesMessage.text = text;
    _profilesMessage.color = error ? ErrorText : _ui.SoftAccent;
    LayoutProfilePages(_profileGallery.rect.width);
  }

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
    top += galleryHeight;
    bool hasMessage = !string.IsNullOrEmpty(_profilesMessage.text);
    _profilesMessage.gameObject.SetActive(hasMessage);
    if (hasMessage)
    {
      float messageHeight = TextHeight(_profilesMessage, width, 20);
      SettingsUiFactory.Place(_profilesMessage.rectTransform, 0, top + 16, width, messageHeight);
      top += 16 + messageHeight;
    }
    if (_fontCard != null && _fontCard.gameObject.activeSelf)
      top += 16 + LayoutFontCard(top + 16, width);
    ((RectTransform)_pages[ProfilesPage].transform).sizeDelta = new Vector2(width, top);
    LayoutNewProfilePopover();
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
