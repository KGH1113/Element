using System;
using System.Collections.Generic;
using Element.Application.Profiles;
using Element.Configuration.Profiles;
using Element.Presentation.Layout;
using Element.Presentation.Overlay.Shared;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static Element.Presentation.Settings.SettingsLocale;

namespace Element.Presentation.Settings.Pages;

/// <summary>
/// General → Profiles: the profile gallery, switching, import and a new
/// profile. Each card renames, duplicates, exports and deletes its profile.
/// Messages sit below the cards, so nothing moves under the pointer.
/// </summary>
internal sealed class ProfilesPage : SettingsPage
{
  private const float CardActionSize = 30,
    CardActionGap = 2,
    CardActionInset = 10,
    CardGlyphSize = 18;

  private readonly IProfiles _profiles;
  private readonly RectTransform _profilesHeading,
    _profileGallery;
  private readonly TextMeshProUGUI _profilesTitle,
    _profilesMessage;
  private readonly Button _importProfile,
    _newProfile;
  private readonly ProfileFontReplacements _fonts;
  private readonly NewProfilePopover _newProfilePopover;
  private readonly List<Button> _profileCards = new();
  private SettingsUiFactory _profileGalleryUi;
  private TextMeshProUGUI _profileLinkDetail;

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

  /// <param name="overlayFonts">The overlay's fonts, to offer replacements after an import; null offers none.</param>
  public ProfilesPage(ISettingsWindow window, IProfiles profiles, Func<OverlayFonts> overlayFonts)
    : base(window, "Profiles")
  {
    _profiles = profiles;
    _profilesHeading = Ui.Rect("Profiles heading", Root, 0, 0, 936, 44);
    _profilesTitle = Ui.Text(L("프로필", "Profiles"), _profilesHeading, 0, 0, 400, 44, 18);
    if (profiles.FilesSupported)
      _importProfile = Ui.Button(L("가져오기", "Import"), _profilesHeading, 0, 0, 120, 44, ImportProfile);
    _newProfile = Ui.Button(L("새 프로필", "New profile"), _profilesHeading, 0, 0, 156, 44, ToggleNewProfile, true);
    _profileGallery = Ui.Rect("Profile gallery", Root, 0, 56, 936, 160);
    _profilesMessage = Ui.Text("", Root, 0, 0, 936, 24, 14, SettingsUiFactory.Muted);
    _profilesMessage.alignment = TextAlignmentOptions.TopLeft;
    _fonts = new ProfileFontReplacements(window, Root, overlayFonts, Relayout);
    _newProfilePopover = new NewProfilePopover(Ui, Window.Frame, Window.Shade, ValidateNewProfileName, CreateProfile);
    RefreshProfileGallery();
  }

  public override string Title => L("프로필", "Profiles");

  /// <summary>The link to this page at the top of General; it names the profile in use.</summary>
  public Button CreateLink(Transform general)
  {
    Button link = DetailLink(
      general,
      L("프로필", "Profiles"),
      "",
      () =>
      {
        _deleteArmedId = null;
        _renaming = null;
        _profilesMessage.text = "";
        RefreshProfileGallery();
        Window.OpenPage(this);
      },
      out _profileLinkDetail
    );
    RefreshProfileLink();
    return link;
  }

  public override void Opened()
  {
    _profilesMessage.text = "";
    Relayout();
  }

  public override void Tick() => _fonts.Tick();

  public override bool HandleEscape() => ProfileRenameTakesEscape() || DismissNewProfile() || _fonts.DismissPopover();

  public override void HidePopovers()
  {
    _fonts.HidePopover();
    HideNewProfile();
  }

  public override void RefreshTheme() => _profileGalleryUi?.RefreshTheme();

  public override void SettingsReplaced()
  {
    // A switch replaces the values the font card points at.
    _fonts.Clear();
    RefreshProfileGallery();
  }

  public override void Dispose()
  {
    _profileGalleryUi?.Dispose();
    _fonts.Dispose();
  }

  private void RefreshProfileLink()
  {
    if (_profileLinkDetail != null)
      _profileLinkDetail.text = L("사용 중: ", "In use: ") + _profiles.Active?.Name;
  }

  private void RefreshProfileGallery()
  {
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
    _profileGalleryUi = new SettingsUiFactory(Window.Font) { ReduceMotion = () => Settings.ReduceUiMotion };
    _profileCards.Clear();
    int count = _profiles.Profiles.Count;
    // The window title already says Profiles; the heading counts them.
    _profilesTitle.text = L("프로필 " + count + "개", count + (count == 1 ? " profile" : " profiles"));
    ElementProfile active = _profiles.Active;
    if (_renaming != null && !Contains(_profiles.Profiles, _renaming))
      _renaming = null;
    foreach (ElementProfile item in _profiles.Profiles)
      _profileCards.Add(CreateCard(item, item == active));
    Ui.RefreshTheme();
    // Rebuilt cards begin at the origin; reflow now, as the preset gallery does.
    Relayout();
    if (_renameInput != null)
      Window.Focus(_renameInput);
  }

  private Button CreateCard(ElementProfile profile, bool inUse)
  {
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
        armed ? SettingsPalette.Error
        : inUse ? Ui.SoftAccent
        : SettingsUiFactory.Muted
    );
    if (inUse)
    {
      var marker = _profileGalleryUi.Panel("Profile in use", card.transform, 0, 0, 3, 140, Ui.SoftAccent, false);
      marker.rectTransform.anchorMin = new Vector2(0, 0);
      marker.rectTransform.anchorMax = new Vector2(0, 1);
      marker.rectTransform.pivot = new Vector2(0, 1);
      marker.rectTransform.sizeDelta = new Vector2(3, -28);
      marker.rectTransform.anchoredPosition = new Vector2(0, -14);
      _profileGalleryUi.Bind(() => marker.color = Ui.SoftAccent);
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
          armed ? SettingsPalette.Error : SettingsUiFactory.Muted,
          armed ? SettingsPalette.Error : SettingsUiFactory.Foreground
        ).Kind = LayoutAction.Delete;
      if (_profiles.FilesSupported)
        CardAction<SettingsExportIcon>(card.transform, "Export profile", slot++, () => ExportProfile(profile));
      CardAction<SettingsDuplicateIcon>(card.transform, "Duplicate profile", slot++, () => DuplicateProfile(profile));
      CardAction<SettingsEditIcon>(card.transform, "Rename profile", slot, () => StartRename(profile));
    }
    return card;
  }

  // A glyph button in the card's top right; slot 0 is the rightmost.
  private T CardAction<T>(
    Transform card,
    string name,
    int slot,
    Action action,
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
    Window.SettingsReplaced();
    // The card shows the switch; only a problem is put into words.
    ShowProfilesMessage(outcome.Ok ? "" : ProfileProblemText.For(outcome, ProfileAction.Switch), true);
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
      Window.SettingsReplaced();
    else
      RefreshProfileGallery();
    ShowProfilesMessage(
      outcome.Ok
        ? L("'" + outcome.Name + "' 프로필을 가져와 바꿨어요.", "Imported '" + outcome.Name + "' and switched to it.")
        : ProfileProblemText.For(outcome, ProfileAction.Import),
      !outcome.Ok
    );
    AfterProfileSave(outcome);
    if (outcome.Ok)
      _fonts.Offer();
  }

  // ---- A new profile, from the popover under its button ----

  private void ToggleNewProfile()
  {
    if (DismissNewProfile())
      return;
    if (!Window.CommitPopovers())
      return;
    Window.HidePopovers();
    _deleteArmedId = null;
    _renaming = null;
    RefreshProfileGallery();
    _newProfilePopover.Show();
    Ui.RefreshTheme();
    LayoutNewProfilePopover();
    Window.Focus(_newProfilePopover.Name);
  }

  private void HideNewProfile()
  {
    if (!_newProfilePopover.Visible)
      return;
    _newProfilePopover.Hide();
    if (!_fonts.PopoverVisible)
      Window.Shade.Hide();
  }

  private bool DismissNewProfile()
  {
    if (!_newProfilePopover.Visible)
      return false;
    HideNewProfile();
    return true;
  }

  private void ValidateNewProfileName()
  {
    string name = _newProfilePopover.Name.text;
    bool valid = _profiles.ValidateName(name, null, out ProfileNameProblem problem);
    _newProfilePopover.CanCreate = valid;
    _newProfilePopover.Problem = valid || name.Trim().Length == 0 ? "" : ProfileProblemText.Name(problem);
    LayoutNewProfilePopover();
  }

  private void CreateProfile()
  {
    if (!_newProfilePopover.Visible)
      return;
    ProfileOutcome outcome = _profiles.Create(_newProfilePopover.Name.text);
    if (outcome.Problem == ProfileProblem.Name)
    {
      _newProfilePopover.Problem = ProfileProblemText.Name(outcome.NameProblem);
      LayoutNewProfilePopover();
      Window.Focus(_newProfilePopover.Name);
      return;
    }
    if (!outcome.Ok)
    {
      _newProfilePopover.Problem = ProfileProblemText.For(outcome, ProfileAction.Save);
      LayoutNewProfilePopover();
      return;
    }
    HideNewProfile();
    Window.SettingsReplaced();
    ShowProfilesMessage(
      L(
        "'" + outcome.Name + "' 프로필을 처음 설정으로 만들었어요. 이제 바꾸는 설정은 이 프로필에 저장됩니다.",
        "Created '" + outcome.Name + "' with the initial settings. Changes you make now are saved to it."
      ),
      false
    );
    AfterProfileSave(outcome);
  }

  private void LayoutNewProfilePopover() => _newProfilePopover.Layout((RectTransform)_newProfile.transform);

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
    _renameProblem.text = shown ? ProfileProblemText.Name(problem) : _renameSummary;
    _renameProblem.color = shown ? SettingsPalette.Error : SettingsUiFactory.Muted;
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
        L("이름을 바꾸지 않았어요. ", "The name wasn't changed. ") + ProfileProblemText.Name(outcome.NameProblem),
        true
      );
    else if (!outcome.Ok)
      ShowProfilesMessage(ProfileProblemText.For(outcome, ProfileAction.Save), true);
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
        : ProfileProblemText.For(outcome, ProfileAction.Save),
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
      Window.SettingsReplaced();
    else
      RefreshProfileGallery();
    ShowProfilesMessage(
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
        : ProfileProblemText.For(outcome, ProfileAction.Export),
      !outcome.Ok
    );
  }

  private void AfterProfileSave(ProfileOutcome outcome)
  {
    if (!outcome.Saved)
      Window.ShowSaveError();
  }

  // Below the cards: a message appearing or going never moves them.
  private void ShowProfilesMessage(string text, bool error)
  {
    _profilesMessage.text = text;
    _profilesMessage.color = error ? SettingsPalette.Error : Ui.SoftAccent;
    Relayout();
  }

  private void Relayout() => Layout(_profileGallery.rect.width, 0);

  public override void Layout(float width, float height)
  {
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
      float messageHeight = SettingsUiFactory.TextHeight(_profilesMessage, width, 20);
      SettingsUiFactory.Place(_profilesMessage.rectTransform, 0, top + 16, width, messageHeight);
      top += 16 + messageHeight;
    }
    if (_fonts.Visible)
      top += 16 + _fonts.Layout(top + 16, width);
    Root.sizeDelta = new Vector2(width, top);
    LayoutNewProfilePopover();
  }
}
