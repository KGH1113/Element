using System.Collections.Generic;
using Element.Configuration.Effects;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static Element.Presentation.Settings.SettingsLocale;

namespace Element.Presentation.Settings.Pages;

/// <summary>Effects: the effect removal switch and a card for each preset, which applies it.</summary>
internal sealed class EffectsPage : SettingsPage
{
  private readonly PresetEditorPage _editor;
  private readonly Button _effectEnabled,
    _newPreset;
  private readonly TextMeshProUGUI _effectStatus,
    _effectHint;
  private readonly RectTransform _effectsHeading,
    _presetGallery;
  private readonly List<Button> _presetCards = new();
  private SettingsUiFactory _galleryUi;

  public EffectsPage(ISettingsWindow window, PresetEditorPage editor)
    : base(window, "Effects")
  {
    _editor = editor;
    _editor.Saved = RefreshPresetGallery;
    Adopt(editor);
    _effectEnabled = Ui.Switch(
      L("이펙트 제거", "Effect removal"),
      "",
      Root,
      0,
      () => Settings.Effects.Enabled,
      value =>
      {
        Settings.Effects.Enabled = value && Settings.Effects.ActivePreset != null;
        Window.Changed();
        RefreshPresetGallery();
      }
    );
    TextMeshProUGUI[] switchLabels = _effectEnabled.GetComponentsInChildren<TextMeshProUGUI>();
    _effectStatus = switchLabels[1];
    Ui.Bind(() =>
    {
      _effectEnabled.interactable = Settings.Effects.Presets.Count > 0;
      _effectStatus.text = Settings.Effects.Enabled
        ? L("적용 프리셋: ", "Active preset: ") + Settings.Effects.ActivePreset?.Name
        : L("끄면 맵의 원래 이펙트가 표시됩니다.", "Off · original map effects are shown.");
    });
    _effectsHeading = Ui.Rect("Presets heading", Root, 0, 100, 936, 44);
    Ui.Text(L("프리셋", "Presets"), _effectsHeading, 0, 0, 400, 44, 18);
    _newPreset = Ui.Button(
      L("프리셋 추가", "New preset"),
      _effectsHeading,
      780,
      0,
      156,
      44,
      () => _editor.Edit(null),
      true
    );
    _presetGallery = Ui.Rect("Preset gallery", Root, 0, 160, 936, 160);
    _effectHint = Ui.Text(
      L(
        "변경 사항은 레벨을 시작하거나 재시작할 때 적용됩니다.",
        "Changes take effect when a level starts or restarts."
      ),
      Root,
      0,
      340,
      936,
      44,
      13,
      SettingsUiFactory.Muted
    );
    RefreshPresetGallery();
  }

  public override string Title => "Effects";

  public override void RefreshTheme() => _galleryUi?.RefreshTheme();

  public override void SettingsReplaced() => RefreshPresetGallery();

  public override void Dispose() => _galleryUi?.Dispose();

  /// <summary>Builds the cards again, as when a preset was applied from outside the window.</summary>
  internal void RefreshPresetGallery()
  {
    for (int i = _presetGallery.childCount - 1; i >= 0; i--)
    {
      GameObject child = _presetGallery.GetChild(i).gameObject;
      child.SetActive(false);
      SettingsUiFactory.Destroy(child);
    }
    _galleryUi?.Dispose();
    _galleryUi = new SettingsUiFactory(Window.Font) { ReduceMotion = () => Settings.ReduceUiMotion };
    _presetCards.Clear();
    foreach (EffectPreset item in Settings.Effects.Presets)
      _presetCards.Add(CreateCard(item));
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
    Ui.RefreshTheme();
    // Rebuilt cards begin at the origin. Reflow immediately, including when only
    // the enabled state changes and no navigation or window resize follows.
    Layout(_presetGallery.rect.width, 0);
  }

  private Button CreateCard(EffectPreset preset)
  {
    Button card = _galleryUi.Button(
      "",
      _presetGallery,
      0,
      0,
      300,
      140,
      () =>
      {
        Settings.Effects.Select(preset.Id);
        Settings.Effects.Enabled = true;
        Window.Changed();
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
    bool selected = Settings.Effects.ActivePresetId == preset.Id;
    var state = _galleryUi.Text(
      selected
        ? (Settings.Effects.Enabled ? L("적용 중", "Active") : L("선택됨 · 꺼짐", "Selected · Off"))
        : L("선택하여 적용", "Click to apply"),
      card.transform,
      18,
      110,
      240,
      22,
      13,
      selected ? Ui.SoftAccent : SettingsUiFactory.Muted
    );
    SettingsUiFactory.StretchWidth(state.rectTransform, 18, 18);
    _galleryUi.Bind(() => state.color = selected ? Ui.SoftAccent : SettingsUiFactory.Muted);
    if (selected)
    {
      var marker = _galleryUi.Panel("Selected preset", card.transform, 0, 0, 3, 140, Ui.SoftAccent, false);
      marker.rectTransform.anchorMin = new Vector2(0, 0);
      marker.rectTransform.anchorMax = new Vector2(0, 1);
      marker.rectTransform.pivot = new Vector2(0, 1);
      marker.rectTransform.sizeDelta = new Vector2(3, -28);
      marker.rectTransform.anchoredPosition = new Vector2(0, -14);
      _galleryUi.Bind(() => marker.color = Ui.SoftAccent);
    }
    Button edit = _galleryUi.GlyphButton(
      "Edit " + preset.Name,
      card.transform,
      36,
      20,
      () => _editor.Edit(preset),
      SettingsUiFactory.Muted,
      SettingsUiFactory.Foreground,
      out SettingsEditIcon _
    );
    ((RectTransform)edit.transform).anchorMin = ((RectTransform)edit.transform).anchorMax = new Vector2(1, 1);
    ((RectTransform)edit.transform).pivot = new Vector2(1, 1);
    ((RectTransform)edit.transform).anchoredPosition = new Vector2(-12, -12);
    return card;
  }

  public override void Layout(float width, float height)
  {
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
    Root.sizeDelta = new Vector2(width, top + galleryHeight + 116);
  }
}
