using System;
using System.Collections.Generic;
using Element.Configuration.Effects;
using Element.Presentation.Layout;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Element.Presentation.Settings;

internal sealed partial class SettingsPanel
{
  private SettingsUiFactory _galleryUi;
  private TMP_FontAsset _effectsFont;
  private RectTransform _presetGallery,
    _effectsHeading,
    _presetHeader,
    _effectTabs,
    _effectOptions,
    _presetShortcutCard;
  private Button _effectEnabled,
    _newPreset,
    _savePreset,
    _deletePreset;
  private TextMeshProUGUI _effectStatus,
    _effectHint,
    _presetError,
    _presetIntro,
    _presetShortcutDescription;
  private TMP_InputField _presetName;
  private readonly List<Button> _presetCards = new();
  private readonly List<Button> _effectTabButtons = new();
  private readonly List<Button> _effectRows = new();
  private readonly List<EffectDefinition> _effectDefinitions = new();
  private EffectPreset _editingPreset,
    _presetDraft;
  private EffectGroup _effectGroup;
  private bool _presetShortcutTab;
  private SettingsValueRow _cameraZoom;
  private SettingsShortcutField _presetShortcut;

  private bool DraftRemovesCamera => _presetDraft != null && (_presetDraft.Effects & RemovedEffects.CameraMotion) != 0;

  private void CreateEffectsPages(TMP_FontAsset font)
  {
    _effectsFont = font;
    Transform gallery = _pages[3].transform;
    _effectEnabled = _ui.Switch(
      L("이펙트 제거", "Effect removal"),
      "",
      gallery,
      0,
      () => _settings.Effects.Enabled,
      value =>
      {
        _settings.Effects.Enabled = value && _settings.Effects.ActivePreset != null;
        Changed();
        RefreshPresetGallery();
      }
    );
    TextMeshProUGUI[] switchLabels = _effectEnabled.GetComponentsInChildren<TextMeshProUGUI>();
    _effectStatus = switchLabels[1];
    _ui.Bind(() =>
    {
      _effectEnabled.interactable = _settings.Effects.Presets.Count > 0;
      _effectStatus.text = _settings.Effects.Enabled
        ? L("적용 프리셋: ", "Active preset: ") + _settings.Effects.ActivePreset?.Name
        : L("끄면 맵의 원래 이펙트가 표시됩니다.", "Off · original map effects are shown.");
    });
    _effectsHeading = _ui.Rect("Presets heading", gallery, 0, 100, 936, 44);
    _ui.Text(L("프리셋", "Presets"), _effectsHeading, 0, 0, 400, 44, 18);
    _newPreset = _ui.Button(
      L("프리셋 추가", "New preset"),
      _effectsHeading,
      780,
      0,
      156,
      44,
      () => EditPreset(null),
      true
    );
    _presetGallery = _ui.Rect("Preset gallery", gallery, 0, 160, 936, 160);
    _effectHint = _ui.Text(
      L(
        "변경 사항은 레벨을 시작하거나 재시작할 때 적용됩니다.",
        "Changes take effect when a level starts or restarts."
      ),
      gallery,
      0,
      340,
      936,
      44,
      13,
      SettingsUiFactory.Muted
    );
    RefreshPresetGallery();

    Transform editor = _pages[7].transform;
    _presetHeader = _ui.Rect("Preset identity", editor, 0, 0, 936, 92);
    _presetName = _ui.Input(
      "Preset name",
      "",
      _presetHeader,
      0,
      0,
      560,
      _ => ValidatePresetName(),
      null,
      EffectSettings.MaximumNameLength
    );
    _presetName.textComponent.textWrappingMode = TextWrappingModes.NoWrap;
    TextMeshProUGUI placeholder = _ui.Text(
      L("프리셋 이름", "Preset name"),
      _presetName.textViewport,
      0,
      0,
      300,
      32,
      16,
      SettingsUiFactory.Muted
    );
    SettingsUiFactory.Stretch(placeholder.rectTransform, 0, 0, 0, 0);
    _presetName.placeholder = placeholder;
    placeholder.alignment = _presetName.textComponent.alignment;
    _savePreset = _ui.Button(L("저장", "Save preset"), _presetHeader, 716, 0, 152, 40, SavePreset, true);
    _deletePreset = ActionIconButton("Delete preset", _presetHeader, LayoutAction.Delete, DeletePreset);
    _presetError = _ui.Text("", _presetHeader, 0, 44, 936, 24, 13, new Color(1, .66f, .53f));
    _presetIntro = _ui.Text(
      L("이 프리셋에서 제거할 이펙트를 선택하세요.", "Choose the effects this preset removes."),
      _presetHeader,
      0,
      68,
      936,
      24,
      14,
      SettingsUiFactory.Muted
    );
    var groups = (EffectGroup[])Enum.GetValues(typeof(EffectGroup));
    // The effect groups, then the shortcut that applies the preset.
    string[] names = new string[groups.Length + 1];
    for (int i = 0; i < groups.Length; i++)
      names[i] = EffectCatalog.GroupName(groups[i], Korean);
    names[groups.Length] = L("단축키", "Shortcut");
    _effectTabs = Tabs(
      editor,
      names,
      _effectTabButtons,
      () => _presetShortcutTab ? groups.Length : (int)_effectGroup,
      value =>
      {
        _presetShortcutTab = value == groups.Length;
        if (!_presetShortcutTab)
          _effectGroup = (EffectGroup)value;
        _presetShortcut.Cancel();
        FitWindow();
      }
    );
    _effectOptions = _ui.Rect("Effect choices", editor, 0, 152, 936, 300);
    foreach (EffectDefinition definition in EffectCatalog.Entries)
    {
      EffectDefinition entry = definition;
      Button row = _ui.Switch(
        Korean ? entry.NameKo : entry.Name,
        Korean ? entry.DescriptionKo : entry.Description,
        _effectOptions,
        0,
        () => _presetDraft != null && (_presetDraft.Effects & entry.Flag) != 0,
        value =>
        {
          if (_presetDraft == null)
            return;
          if (value)
            _presetDraft.Effects |= entry.Flag;
          else
            _presetDraft.Effects &= ~entry.Flag;
        }
      );
      _effectRows.Add(row);
      _effectDefinitions.Add(entry);
    }
    _cameraZoom = new SettingsValueRow(
      _ui,
      _effectOptions,
      "Camera zoom",
      L("카메라 확대", "Camera zoom"),
      "%",
      EffectPreset.MinimumCameraZoom,
      EffectPreset.MaximumCameraZoom,
      10,
      () => _presetDraft?.CameraZoom ?? EffectPreset.DefaultCameraZoom,
      zoom =>
      {
        if (_presetDraft != null)
          _presetDraft.CameraZoom = zoom;
      }
    );
    // Turning camera movement on or off in the draft enables the zoom with it.
    _ui.Bind(() => _cameraZoom.Sync(DraftRemovesCamera));
    CreatePresetShortcutCard(editor);
  }

  private void CreatePresetShortcutCard(Transform editor)
  {
    _presetShortcutCard = _ui.Panel(
      "Preset shortcut",
      editor,
      0,
      152,
      936,
      160,
      SettingsUiFactory.Surface
    ).rectTransform;
    TextMeshProUGUI title = _ui.Text(L("단축키", "Shortcut"), _presetShortcutCard, 16, 16, 700, 28, 17);
    SettingsUiFactory.StretchWidth(title.rectTransform, 16, 16);
    _presetShortcutDescription = _ui.Text(
      L(
        "누르면 이 프리셋을 바로 적용합니다. 레벨을 플레이하는 중에는 동작하지 않습니다.",
        "Press it to apply this preset right away. It does nothing while you play a level."
      ),
      _presetShortcutCard,
      16,
      48,
      904,
      24,
      14,
      SettingsUiFactory.Muted
    );
    SettingsUiFactory.StretchWidth(_presetShortcutDescription.rectTransform, 16, 16);
    _presetShortcut = new SettingsShortcutField(
      _ui,
      _presetShortcutCard,
      () => _presetDraft?.ShortcutKey ?? KeyCode.None,
      () => _presetDraft?.ShortcutModifier ?? KeyCode.None,
      AssignPresetShortcut,
      "Remove shortcut",
      LayoutAction.Close,
      () =>
      {
        _presetDraft?.ClearShortcut();
        _presetShortcut.Refresh();
        FitWindow();
      },
      L("왼쪽 Alt + 1", "Left Alt + 1"),
      FitWindow,
      L("눌러서 단축키 지정", "Click to set a shortcut"),
      actionNeedsShortcut: true
    );
  }

  private string AssignPresetShortcut(KeyCode key, KeyCode modifier)
  {
    if (_presetDraft == null)
      return "";
    if (key == _settings.SettingsShortcutKey && modifier == _settings.SettingsShortcutModifier)
      return L(
        "설정 화면을 여는 단축키예요. 다른 키를 눌러주세요.",
        "This shortcut opens settings. Press another one."
      );
    EffectPreset owner = _settings.Effects.FindShortcut(key, modifier, _editingPreset?.Id);
    if (owner != null)
      return ShortcutInUse(owner);
    _presetDraft.ShortcutKey = key;
    _presetDraft.ShortcutModifier = modifier;
    return null;
  }

  private static string ShortcutInUse(EffectPreset owner) =>
    L(
      "'" + owner.Name + "' 프리셋이 이 단축키를 쓰고 있어요. 다른 키를 눌러주세요.",
      "'" + owner.Name + "' already uses this shortcut. Press another one."
    );

  /// <summary>Shows a preset applied from outside the window, such as by its shortcut.</summary>
  public void RefreshPresets() => RefreshPresetGallery();

  private Button ActionIconButton(string name, Transform parent, LayoutAction icon, Action action)
  {
    Button button = _ui.Button("", parent, 0, 0, 40, 40, action);
    button.gameObject.name = name;
    button.GetComponentInChildren<TextMeshProUGUI>().gameObject.SetActive(false);
    var graphic = _ui.Rect(name + " icon", button.transform, 10, 10, 20, 20)
      .gameObject.AddComponent<LayoutActionIcon>();
    graphic.Kind = icon;
    graphic.raycastTarget = false;
    graphic.color = SettingsUiFactory.Foreground;
    return button;
  }

  private void RefreshPresetGallery()
  {
    if (_presetGallery == null)
      return;
    for (int i = _presetGallery.childCount - 1; i >= 0; i--)
    {
      GameObject child = _presetGallery.GetChild(i).gameObject;
      child.SetActive(false);
      SettingsUiFactory.Destroy(child);
    }
    _galleryUi?.Dispose();
    _galleryUi = new SettingsUiFactory(_effectsFont) { ReduceMotion = () => _settings.ReduceUiMotion };
    _presetCards.Clear();
    foreach (EffectPreset item in _settings.Effects.Presets)
    {
      EffectPreset preset = item;
      Button card = _galleryUi.Button(
        "",
        _presetGallery,
        0,
        0,
        300,
        140,
        () =>
        {
          _settings.Effects.Select(preset.Id);
          _settings.Effects.Enabled = true;
          Changed();
          RefreshPresetGallery();
        }
      );
      card.gameObject.name = "Preset: " + preset.Name;
      card.GetComponentInChildren<TextMeshProUGUI>().gameObject.SetActive(false);
      var title = _galleryUi.Text(preset.Name, card.transform, 18, 18, 220, 54, 20);
      SettingsUiFactory.StretchWidth(title.rectTransform, 18, 56);
      title.alignment = TextAlignmentOptions.TopLeft;
      var count = _galleryUi.Text(
        EffectCatalog.Count(preset.Effects) + L("개 효과 제거", " effects removed"),
        card.transform,
        18,
        82,
        220,
        22,
        13,
        SettingsUiFactory.Muted
      );
      SettingsUiFactory.StretchWidth(count.rectTransform, 18, 18);
      if (preset.HasShortcut)
      {
        var shortcut = _galleryUi.Text(
          ShortcutKeys.TextLabel(preset.ShortcutKey, preset.ShortcutModifier),
          card.transform,
          18,
          82,
          220,
          22,
          13,
          SettingsUiFactory.Muted
        );
        shortcut.alignment = TextAlignmentOptions.MidlineRight;
        float shortcutWidth = Mathf.Min(shortcut.preferredWidth, 150);
        SettingsUiFactory.StretchWidth(shortcut.rectTransform, 18, 18);
        SettingsUiFactory.StretchWidth(count.rectTransform, 18, 18 + shortcutWidth + 12);
      }
      bool selected = _settings.Effects.ActivePresetId == preset.Id;
      var state = _galleryUi.Text(
        selected
          ? (_settings.Effects.Enabled ? L("적용 중", "Active") : L("선택됨 · 꺼짐", "Selected · Off"))
          : L("선택하여 적용", "Click to apply"),
        card.transform,
        18,
        110,
        240,
        22,
        13,
        selected ? _ui.SoftAccent : SettingsUiFactory.Muted
      );
      SettingsUiFactory.StretchWidth(state.rectTransform, 18, 18);
      _galleryUi.Bind(() => state.color = selected ? _ui.SoftAccent : SettingsUiFactory.Muted);
      if (selected)
      {
        var marker = _galleryUi.Panel("Selected preset", card.transform, 0, 0, 3, 140, _ui.SoftAccent, false);
        marker.rectTransform.anchorMin = new Vector2(0, 0);
        marker.rectTransform.anchorMax = new Vector2(0, 1);
        marker.rectTransform.pivot = new Vector2(0, 1);
        marker.rectTransform.sizeDelta = new Vector2(3, -28);
        marker.rectTransform.anchoredPosition = new Vector2(0, -14);
        _galleryUi.Bind(() => marker.color = _ui.SoftAccent);
      }
      Button edit = _galleryUi.Button("", card.transform, 250, 12, 36, 36, () => EditPreset(preset));
      edit.gameObject.name = "Edit " + preset.Name;
      edit.GetComponentInChildren<TextMeshProUGUI>().gameObject.SetActive(false);
      var editIcon = _galleryUi
        .Rect("Edit icon", edit.transform, 8, 8, 20, 20)
        .gameObject.AddComponent<SettingsEditIcon>();
      editIcon.color = SettingsUiFactory.Muted;
      editIcon.raycastTarget = false;
      ((RectTransform)edit.transform).anchorMin = ((RectTransform)edit.transform).anchorMax = new Vector2(1, 1);
      ((RectTransform)edit.transform).pivot = new Vector2(1, 1);
      ((RectTransform)edit.transform).anchoredPosition = new Vector2(-12, -12);
      _presetCards.Add(card);
    }
    if (_presetCards.Count == 0)
      _galleryUi.Text(
        L("첫 프리셋을 만들어 보세요.", "Create your first preset."),
        _presetGallery,
        0,
        28,
        600,
        44,
        18,
        SettingsUiFactory.Muted
      );
    _ui.RefreshTheme();
    // Rebuilt cards begin at the origin. Reflow immediately, including when only
    // the enabled state changes and no navigation or window resize follows.
    LayoutEffectsPages(_presetGallery.rect.width);
  }

  private void EditPreset(EffectPreset preset)
  {
    _editingPreset = preset;
    _presetDraft = preset?.Copy() ?? new EffectPreset();
    _presetName.SetTextWithoutNotify(_presetDraft.Name);
    _presetError.text = "";
    _presetIntro.gameObject.SetActive(true);
    _savePreset.interactable = preset != null;
    _deletePreset.gameObject.SetActive(preset != null);
    _effectGroup = EffectGroup.Screen;
    _presetShortcutTab = false;
    _presetShortcut.Cancel();
    _presetShortcut.Refresh();
    _cameraZoom.Sync(DraftRemovesCamera);
    _ui.RefreshTheme();
    NavigateTo(7);
    if (preset == null)
    {
      _presetName.Select();
      _presetName.ActivateInputField();
    }
  }

  private void ValidatePresetName()
  {
    if (_savePreset == null)
      return;
    bool valid = _settings.Effects.ValidateName(_presetName.text, _editingPreset?.Id, out _, out string error);
    _savePreset.interactable = valid;
    _presetError.text = valid || _presetName.text.Length == 0 ? "" : PresetNameError(error);
    if (_presetIntro != null)
      _presetIntro.gameObject.SetActive(_presetError.text.Length == 0);
  }

  private static string PresetNameError(string error) =>
    !Korean ? error
    : error.StartsWith("Give") ? "프리셋 이름을 입력하세요."
    : error.StartsWith("Use") ? "이름은 40자 이내로 입력하세요."
    : "같은 이름의 프리셋이 있어요. 다른 이름을 입력하세요.";

  private void SavePreset()
  {
    if (!_settings.Effects.ValidateName(_presetName.text, _editingPreset?.Id, out string name, out string error))
    {
      _presetError.text = PresetNameError(error);
      _presetIntro.gameObject.SetActive(false);
      _presetName.Select();
      return;
    }
    if (_editingPreset == null)
    {
      if (!_settings.Effects.TryAdd(name, out _editingPreset, out error))
        return;
    }
    else
      _settings.Effects.TryRename(_editingPreset.Id, name, out _);
    _editingPreset.Effects = _presetDraft.Effects;
    _editingPreset.CameraZoom = _presetDraft.CameraZoom;
    _editingPreset.ShortcutKey = _presetDraft.ShortcutKey;
    _editingPreset.ShortcutModifier = _presetDraft.ShortcutModifier;
    Changed();
    RefreshPresetGallery();
    SelectPage(3);
  }

  private void DeletePreset()
  {
    if (_editingPreset == null)
      return;
    _settings.Effects.Remove(_editingPreset.Id);
    Changed();
    RefreshPresetGallery();
    SelectPage(3);
  }

  private void LayoutEffectsPages(float width)
  {
    if (_effectsHeading == null)
      return;
    SettingsUiFactory.Place((RectTransform)_effectEnabled.transform, 0, 0, width, width < 480 ? 116 : 88);
    float top = width < 480 ? 128 : 100;
    SettingsUiFactory.Place(_effectsHeading, 0, top, width, 44);
    SettingsUiFactory.Place((RectTransform)_newPreset.transform, width - 156, 0, 156, 44);
    int columns =
      width >= 600 ? 3
      : width >= 440 ? 2
      : 1;
    float cardWidth = (width - (columns - 1) * 12) / columns;
    float galleryHeight = Mathf.Max(152, Mathf.CeilToInt(_presetCards.Count / (float)columns) * 152 - 12);
    SettingsUiFactory.Place(_presetGallery, 0, top + 60, width, galleryHeight);
    for (int i = 0; i < _presetCards.Count; i++)
      SettingsUiFactory.Place(
        (RectTransform)_presetCards[i].transform,
        i % columns * (cardWidth + 12),
        i / columns * 152,
        cardWidth,
        140
      );
    SettingsUiFactory.Place(_effectHint.rectTransform, 0, top + galleryHeight + 72, width, 44);
    ((RectTransform)_pages[3].transform).sizeDelta = new Vector2(width, top + galleryHeight + 116);
    if (_presetHeader == null || _presetShortcutCard == null)
      return;
    bool narrow = width < 600;
    float identityHeight = narrow ? 128 : 76;
    SettingsUiFactory.Place(_presetHeader, 0, 0, width, identityHeight);
    SettingsUiFactory.Place((RectTransform)_presetName.transform, 0, 0, narrow ? width : width - 216, 40);
    SettingsUiFactory.Place(
      (RectTransform)_savePreset.transform,
      narrow ? width - 152 : width - 200,
      narrow ? 52 : 0,
      152,
      40
    );
    SettingsUiFactory.Place((RectTransform)_deletePreset.transform, narrow ? 0 : width - 40, narrow ? 52 : 0, 40, 40);
    SettingsUiFactory.Place(_presetError.rectTransform, 0, narrow ? 104 : 52, width, 24);
    SettingsUiFactory.Place(_presetIntro.rectTransform, 0, narrow ? 104 : 52, width, 24);
    // Two rows on compact screens keep the six tab labels legible.
    int tabColumns = narrow ? 3 : _effectTabButtons.Count;
    int tabRows = Mathf.CeilToInt(_effectTabButtons.Count / (float)tabColumns);
    float tabsHeight = tabRows * 40 + 8;
    SettingsUiFactory.Place(_effectTabs, 0, identityHeight + 12, width, tabsHeight);
    float tabWidth = (width - 8) / tabColumns;
    for (int i = 0; i < _effectTabButtons.Count; i++)
      SettingsUiFactory.Place(
        (RectTransform)_effectTabButtons[i].transform,
        4 + i % tabColumns * tabWidth,
        4 + i / tabColumns * 40,
        tabWidth,
        36
      );
    float optionsTop = identityHeight + tabsHeight + 24;
    _presetShortcutCard.gameObject.SetActive(_presetShortcutTab);
    if (_presetShortcutTab)
    {
      LayoutPresetShortcut(optionsTop, width);
      return;
    }
    int optionColumns = width >= 600 ? 2 : 1;
    float optionWidth = (width - (optionColumns - 1) * 12) / optionColumns;
    float optionHeight = width < 600 ? 116 : 100;
    // Camera movement leads the Screen tab with its zoom right under it.
    _effectOptions.gameObject.SetActive(true);
    bool screen = _effectGroup == EffectGroup.Screen;
    int camera = _effectDefinitions.FindIndex(entry => entry.Flag == RemovedEffects.CameraMotion);
    _cameraZoom.SetVisible(screen);
    float gridTop = 0;
    if (screen && camera >= 0)
    {
      SettingsUiFactory.Place((RectTransform)_effectRows[camera].transform, 0, 0, width, optionHeight);
      gridTop = optionHeight + 12;
      _cameraZoom.Layout(0, gridTop, width);
      gridTop += SettingsValueRow.Height + 12;
    }
    int visible = 0;
    for (int i = 0; i < _effectRows.Count; i++)
    {
      bool show = _effectDefinitions[i].Group == _effectGroup;
      _effectRows[i].gameObject.SetActive(show);
      if (!show || i == camera)
        continue;
      SettingsUiFactory.Place(
        (RectTransform)_effectRows[i].transform,
        visible % optionColumns * (optionWidth + 12),
        gridTop + visible / optionColumns * (optionHeight + 12),
        optionWidth,
        optionHeight
      );
      visible++;
    }
    float optionsHeight = gridTop + Mathf.CeilToInt(visible / (float)optionColumns) * (optionHeight + 12) - 12;
    SettingsUiFactory.Place(_effectOptions, 0, optionsTop, width, optionsHeight);
    ((RectTransform)_pages[7].transform).sizeDelta = new Vector2(width, optionsTop + optionsHeight);
  }

  private void LayoutPresetShortcut(float top, float width)
  {
    _effectOptions.gameObject.SetActive(false);
    _presetShortcut.Refresh();
    SettingsUiFactory.Place(_presetShortcutCard, 0, top, width, 160);
    // The description wraps on narrow windows; the field follows its height.
    float descriptionHeight = Mathf.Max(20, _presetShortcutDescription.GetPreferredValues(width - 32, 0).y);
    SettingsUiFactory.Place(_presetShortcutDescription.rectTransform, 16, 48, width - 32, descriptionHeight);
    float fieldBottom = _presetShortcut.Layout(48 + descriptionHeight + 16, width);
    float cardHeight = fieldBottom + (_presetShortcut.Capturing ? 16 : 20);
    _presetShortcutCard.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, cardHeight);
    ((RectTransform)_pages[7].transform).sizeDelta = new Vector2(width, top + cardHeight);
  }
}

internal sealed class SettingsEditIcon : SettingsVectorGraphic
{
  protected override void OnPopulateMesh(VertexHelper mesh)
  {
    mesh.Clear();
    BeginPath();
    PathPoint(4, 16);
    PathPoint(15, 5);
    PathPoint(19, 9);
    PathPoint(8, 20);
    PathPoint(3, 21);
    PathPoint(4, 16);
    Stroke(mesh);
    Line(mesh, 12, 8, 16, 12);
  }
}
