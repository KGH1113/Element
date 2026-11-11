using System;
using System.Collections.Generic;
using Element.Application.Effects;
using Element.Configuration.Effects;
using Element.Presentation.Ui.Graphics;
using Element.Presentation.Ui.Theme;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static Element.Presentation.Ui.Theme.UiLocale;

namespace Element.Presentation.Settings.Pages;

/// <summary>
/// Effects → a preset: its name, the effects it removes in tabs by group,
/// the camera zoom and the shortcut that applies it. Edits wait in a draft
/// until Save.
/// </summary>
internal sealed class PresetEditorPage : SettingsPage
{
  private readonly EffectPresetLibrary _presets;
  private readonly RectTransform _presetHeader,
    _effectOptions,
    _presetShortcutCard;
  private readonly TMP_InputField _presetName;
  private readonly Button _savePreset,
    _deletePreset;
  private readonly TextMeshProUGUI _presetError,
    _presetIntro,
    _presetShortcutDescription;
  private readonly SettingsSegmentedControl _effectTabs;
  private readonly List<Button> _effectRows = new();
  private readonly List<EffectDefinition> _effectDefinitions = new();
  private readonly SettingsValueRow _cameraZoom;
  private readonly SettingsShortcutField _presetShortcut;
  private EffectPreset _editingPreset,
    _presetDraft;
  private EffectGroup _effectGroup;
  private bool _presetShortcutTab;

  /// <summary>After a save or delete, before the window returns to the gallery.</summary>
  public Action Saved;

  public PresetEditorPage(ISettingsWindow window)
    : base(window, "Preset editor")
  {
    _presets = new EffectPresetLibrary(Settings.Effects);
    _presetHeader = Ui.Rect("Preset identity", Root, 0, 0, 936, 92);
    _presetName = Ui.Input(
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
    TextMeshProUGUI placeholder = Ui.Text(
      L("프리셋 이름", "Preset name"),
      _presetName.textViewport,
      0,
      0,
      300,
      32,
      16,
      UiPalette.Muted
    );
    SettingsUiFactory.Stretch(placeholder.rectTransform, 0, 0, 0, 0);
    _presetName.placeholder = placeholder;
    placeholder.alignment = _presetName.textComponent.alignment;
    _savePreset = Ui.Button(L("저장", "Save preset"), _presetHeader, 716, 0, 152, 40, SavePreset, true);
    _deletePreset = ActionIconButton("Delete preset", _presetHeader, UiIconKind.Delete, DeletePreset);
    _presetError = Ui.Text("", _presetHeader, 0, 44, 936, 24, 13, UiPalette.Error);
    _presetIntro = Ui.Text(
      L("이 프리셋에서 제거할 이펙트를 선택하세요.", "Choose the effects this preset removes."),
      _presetHeader,
      0,
      68,
      936,
      24,
      14,
      UiPalette.Muted
    );
    var groups = (EffectGroup[])Enum.GetValues(typeof(EffectGroup));
    // The effect groups, then the shortcut that applies the preset.
    string[] names = new string[groups.Length + 1];
    for (int i = 0; i < groups.Length; i++)
      names[i] = EffectCatalog.GroupName(groups[i], Korean);
    names[groups.Length] = L("단축키", "Shortcut");
    _effectTabs = Tabs(
      Root,
      names,
      value =>
      {
        _presetShortcutTab = value == groups.Length;
        if (!_presetShortcutTab)
          _effectGroup = (EffectGroup)value;
        _presetShortcut.Cancel();
        Window.FitWindow();
      }
    );
    _effectOptions = Ui.Rect("Effect choices", Root, 0, 152, 936, 300);
    foreach (EffectDefinition definition in EffectCatalog.Entries)
    {
      EffectDefinition entry = definition;
      Button row = Ui.Switch(
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
      Ui,
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
    Ui.Bind(() => _cameraZoom.Sync(DraftRemovesCamera));
    _presetShortcutCard = Ui.Panel("Preset shortcut", Root, 0, 152, 936, 160, UiPalette.Surface).rectTransform;
    TextMeshProUGUI title = Ui.Text(L("단축키", "Shortcut"), _presetShortcutCard, 16, 16, 700, 28, 17);
    SettingsUiFactory.StretchWidth(title.rectTransform, 16, 16);
    _presetShortcutDescription = Ui.Text(
      L(
        "누르면 이 프리셋을 적용하고, 한 번 더 누르면 이펙트 제거를 끕니다. 플레이 중에는 동작하지 않습니다.",
        "Press it to apply this preset, and again to turn effect removal off. It does nothing during play."
      ),
      _presetShortcutCard,
      16,
      48,
      904,
      24,
      14,
      UiPalette.Muted
    );
    SettingsUiFactory.StretchWidth(_presetShortcutDescription.rectTransform, 16, 16);
    _presetShortcut = new SettingsShortcutField(
      Ui,
      _presetShortcutCard,
      () => _presetDraft?.ShortcutKey ?? KeyCode.None,
      () => _presetDraft?.ShortcutModifier ?? KeyCode.None,
      AssignPresetShortcut,
      "Remove shortcut",
      UiIconKind.Close,
      () =>
      {
        _presetDraft?.ClearShortcut();
        _presetShortcut.Refresh();
        Window.FitWindow();
      },
      L("왼쪽 Alt + 1", "Left Alt + 1"),
      Window.FitWindow,
      L("눌러서 단축키 지정", "Click to set a shortcut"),
      actionNeedsShortcut: true
    );
  }

  public override string Title =>
    _editingPreset == null ? L("새 프리셋", "New preset") : L("프리셋 편집", "Edit preset");

  private bool DraftRemovesCamera => _presetDraft != null && (_presetDraft.Effects & RemovedEffects.CameraMotion) != 0;

  public override bool TickCapture()
  {
    if (!_presetShortcut.Capturing)
      return false;
    _presetShortcut.Tick();
    return true;
  }

  public override void CancelCapture() => _presetShortcut.Cancel();

  /// <summary>Opens a draft of <paramref name="preset"/>, or of a new preset when null.</summary>
  internal void Edit(EffectPreset preset)
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
    _effectTabs.Selected = (int)_effectGroup;
    _presetShortcut.Cancel();
    _presetShortcut.Refresh();
    _cameraZoom.Sync(DraftRemovesCamera);
    Ui.RefreshTheme();
    Window.OpenPage(this);
    if (preset == null)
    {
      _presetName.Select();
      _presetName.ActivateInputField();
    }
  }

  private string AssignPresetShortcut(KeyCode key, KeyCode modifier)
  {
    if (_presetDraft == null)
      return "";
    if (Settings.ShortcutTaken(key, modifier, _presetDraft.Id, out EffectPreset owner))
      return ShortcutTakenText.For(owner);
    _presetDraft.ShortcutKey = key;
    _presetDraft.ShortcutModifier = modifier;
    return null;
  }

  private Button ActionIconButton(string name, Transform parent, UiIconKind icon, Action action)
  {
    Button button = Ui.Button("", parent, 0, 0, 40, 40, action);
    button.gameObject.name = name;
    button.GetComponentInChildren<TextMeshProUGUI>().gameObject.SetActive(false);
    var graphic = Ui.Rect(name + " icon", button.transform, 10, 10, 20, 20).gameObject.AddComponent<UiIcon>();
    graphic.Kind = icon;
    graphic.raycastTarget = false;
    graphic.color = UiPalette.Foreground;
    return button;
  }

  private void ValidatePresetName()
  {
    if (_savePreset == null)
      return;
    bool valid = _presets.ValidateName(_presetName.text, _editingPreset?.Id, out _, out PresetNameProblem problem);
    _savePreset.interactable = valid;
    _presetError.text = valid || _presetName.text.Length == 0 ? "" : PresetNameText(problem);
    if (_presetIntro != null)
      _presetIntro.gameObject.SetActive(_presetError.text.Length == 0);
  }

  private static string PresetNameText(PresetNameProblem problem) =>
    problem switch
    {
      PresetNameProblem.Empty => L("프리셋 이름을 입력하세요.", "Give this preset a name."),
      PresetNameProblem.TooLong => L(
        "이름은 " + EffectSettings.MaximumNameLength + "자 이내로 입력하세요.",
        "Use " + EffectSettings.MaximumNameLength + " characters or fewer."
      ),
      _ => L(
        "같은 이름의 프리셋이 있어요. 다른 이름을 입력하세요.",
        "A preset already uses this name. Choose another."
      ),
    };

  private void SavePreset()
  {
    if (!_presets.ValidateName(_presetName.text, _editingPreset?.Id, out string name, out PresetNameProblem problem))
    {
      _presetError.text = PresetNameText(problem);
      _presetIntro.gameObject.SetActive(false);
      _presetName.Select();
      return;
    }
    if (_editingPreset == null)
    {
      if (!_presets.TryAdd(name, out _editingPreset, out _))
        return;
    }
    else
      _presets.TryRename(_editingPreset.Id, name, out _);
    _editingPreset.Effects = _presetDraft.Effects;
    _editingPreset.CameraZoom = _presetDraft.CameraZoom;
    _editingPreset.ShortcutKey = _presetDraft.ShortcutKey;
    _editingPreset.ShortcutModifier = _presetDraft.ShortcutModifier;
    Window.Changed();
    Saved?.Invoke();
    Window.ShowPage(Parent);
  }

  private void DeletePreset()
  {
    if (_editingPreset == null)
      return;
    _presets.Remove(_editingPreset.Id);
    Window.Changed();
    Saved?.Invoke();
    Window.ShowPage(Parent);
  }

  public override void Layout(float width, float height)
  {
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
    int tabColumns = narrow ? 3 : _effectTabs.Buttons.Count;
    int tabRows = Mathf.CeilToInt(_effectTabs.Buttons.Count / (float)tabColumns);
    float tabsHeight = tabRows * 40 + 8;
    _effectTabs.Place(0, identityHeight + 12, width, tabsHeight, tabColumns);
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
    Root.sizeDelta = new Vector2(width, optionsTop + optionsHeight);
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
    Root.sizeDelta = new Vector2(width, top + cardHeight);
  }
}
