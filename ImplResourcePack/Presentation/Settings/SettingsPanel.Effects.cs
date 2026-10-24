using System;
using System.Collections.Generic;
using ImplResourcePack.Configuration.Effects;
using ImplResourcePack.Presentation.Layout;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ImplResourcePack.Presentation.Settings;

internal sealed partial class SettingsPanel
{
  private SettingsUiFactory _galleryUi;
  private TMP_FontAsset _effectsFont;
  private RectTransform _presetGallery,
    _effectsHeading,
    _presetHeader,
    _effectTabs,
    _effectOptions;
  private Button _effectEnabled,
    _newPreset,
    _savePreset,
    _deletePreset;
  private TextMeshProUGUI _effectStatus,
    _effectHint,
    _presetError,
    _presetIntro;
  private TMP_InputField _presetName;
  private readonly List<Button> _presetCards = new();
  private readonly List<Button> _effectTabButtons = new();
  private readonly List<Button> _effectRows = new();
  private readonly List<EffectDefinition> _effectDefinitions = new();
  private EffectPreset _editingPreset,
    _presetDraft;
  private EffectGroup _effectGroup;

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
    string[] names = new string[groups.Length];
    for (int i = 0; i < names.Length; i++)
      names[i] = EffectCatalog.GroupName(groups[i], Korean);
    _effectTabs = Tabs(
      editor,
      names,
      _effectTabButtons,
      () => (int)_effectGroup,
      value =>
      {
        _effectGroup = (EffectGroup)value;
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
  }

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
    if (_presetHeader == null)
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
    // Two rows on compact screens keep five category labels legible.
    int tabColumns = narrow ? 3 : 5;
    int tabRows = narrow ? 2 : 1;
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
    int optionColumns = width >= 600 ? 2 : 1;
    float optionWidth = (width - (optionColumns - 1) * 12) / optionColumns;
    float optionHeight = width < 600 ? 116 : 100;
    int visible = 0;
    for (int i = 0; i < _effectRows.Count; i++)
    {
      bool show = _effectDefinitions[i].Group == _effectGroup;
      _effectRows[i].gameObject.SetActive(show);
      if (!show)
        continue;
      SettingsUiFactory.Place(
        (RectTransform)_effectRows[i].transform,
        visible % optionColumns * (optionWidth + 12),
        visible / optionColumns * (optionHeight + 12),
        optionWidth,
        optionHeight
      );
      visible++;
    }
    float optionsHeight = Mathf.CeilToInt(visible / (float)optionColumns) * (optionHeight + 12) - 12;
    float optionsTop = identityHeight + tabsHeight + 24;
    SettingsUiFactory.Place(_effectOptions, 0, optionsTop, width, optionsHeight);
    ((RectTransform)_pages[7].transform).sizeDelta = new Vector2(width, optionsTop + optionsHeight);
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
