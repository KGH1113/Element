using System;
using System.Collections.Generic;
using ImplResourcePack.Configuration;
using ImplResourcePack.Presentation.Overlay.Shared;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ImplResourcePack.Presentation.Settings;

internal sealed partial class SettingsPanel
{
  private Button _planetEnabled,
    _planetProfileEnabled;
  private RectTransform _planetTabs,
    _planetPreview,
    _planetComponents;
  private readonly List<Button> _planetTabButtons = new();
  private readonly List<Button> _planetColorButtons = new();
  private readonly List<Button> _planetSourceButtons = new();
  private readonly List<RectTransform> _planetRows = new();
  private readonly Func<int, PlanetAppearanceProfile, Texture> _renderPlanetPreview;
  private RawImage _planetImage;
  private float _nextPlanetPreview;
  private TextMeshProUGUI _planetPreviewLabel;
  private int _planetIndex;
  private SettingsColorPicker _planetPicker;
  private Button _pickerShade;
  private RectTransform _pickerAnchor;

  private PlanetAppearanceProfile CurrentPlanet =>
    _planetIndex == 0 ? _settings.Gameplay.Planets.Red
    : _planetIndex == 1 ? _settings.Gameplay.Planets.Blue
    : _settings.Gameplay.Planets.Green;

  private PlanetComponentAppearance PlanetPart(int index) =>
    index == 0 ? CurrentPlanet.Body
    : index == 1 ? CurrentPlanet.Tail
    : CurrentPlanet.Ring;

  private void CreatePlanetPage()
  {
    Transform page = _pages[6].transform;
    _planetEnabled = _ui.Switch(
      L("행성 꾸미기", "Custom planets"),
      L("행성별 색상과 불투명도를 직접 설정합니다.", "Set color and opacity for each planet."),
      page,
      0,
      () => _settings.Gameplay.Planets.Enabled,
      value =>
      {
        _settings.Gameplay.Planets.Enabled = value;
        Changed();
      }
    );
    _planetTabs = Tabs(
      page,
      new[] { L("불", "Fire"), L("얼음", "Ice"), L("세 번째 공", "Third planet") },
      _planetTabButtons,
      () => _planetIndex,
      value =>
      {
        HidePopover();
        _planetIndex = value;
        _ui.RefreshTheme();
      }
    );
    _planetPreview = _ui.Panel("Planet preview", page, 0, 156, 300, 248, SettingsUiFactory.Surface).rectTransform;
    RectTransform artwork = _ui.Rect("Game planet preview", _planetPreview, 16, 16, 268, 168);
    _planetImage = artwork.gameObject.AddComponent<RawImage>();
    _planetImage.raycastTarget = false;
    _planetImage.enabled = false;
    _planetPreviewLabel = _ui.Text(
      L("색상 미리보기", "Color preview"),
      _planetPreview,
      16,
      198,
      268,
      28,
      13,
      SettingsUiFactory.Muted
    );
    _planetPreviewLabel.alignment = TextAlignmentOptions.Midline;
    _planetComponents = _ui.Rect("Planet components", page, 316, 156, 620, 248);
    _planetProfileEnabled = _ui.Switch(
      L("이 행성에 적용", "Customize this planet"),
      "",
      _planetComponents,
      0,
      () => CurrentPlanet.Enabled,
      value =>
      {
        CurrentPlanet.Enabled = value;
        Changed();
      }
    );
    // A compact single-line switch does not reserve a second empty copy line.
    TextMeshProUGUI[] profileLabels = _planetProfileEnabled.GetComponentsInChildren<TextMeshProUGUI>();
    profileLabels[1].gameObject.SetActive(false);
    string[] names = { L("공", "Body"), L("꼬리", "Trail"), L("고리", "Ring") };
    for (int i = 0; i < names.Length; i++)
    {
      int part = i;
      var row = _ui.Panel(
        names[i],
        _planetComponents,
        0,
        72 + i * 68,
        620,
        60,
        SettingsUiFactory.Surface
      ).rectTransform;
      _planetRows.Add(row);
      _ui.Text(names[i], row, 16, 10, 112, 40, 16);
      Button source = _ui.Button(
        "",
        row,
        132,
        10,
        120,
        40,
        () =>
        {
          PlanetPart(part).OverrideColor = !PlanetPart(part).OverrideColor;
          Changed();
        }
      );
      source.gameObject.name = names[i] + " color source";
      _planetSourceButtons.Add(source);
      Button colorButton = _ui.Button("", row, 264, 10, 180, 40, () => OpenPlanetColor(part));
      colorButton.gameObject.name = "Edit " + names[i] + " color and opacity";
      _planetColorButtons.Add(colorButton);
      TextMeshProUGUI value = colorButton.GetComponentInChildren<TextMeshProUGUI>();
      SettingsUiFactory.Stretch(value.rectTransform, 40, 0, 10, 0);
      value.fontSize = 14;
      var swatch = _ui.Panel("Color swatch", colorButton.transform, 12, 12, 16, 16, Color.white);
      _ui.Bind(() =>
      {
        var appearance = PlanetPart(part);
        Color parsed = ParsePlanetColor(appearance);
        swatch.color = new Color(parsed.r, parsed.g, parsed.b, 1);
        value.text = appearance.Opacity.ToString("0") + "%";
        source.GetComponentInChildren<TextMeshProUGUI>().text = appearance.OverrideColor
          ? L("직접 지정", "Custom color")
          : L("맵 색상", "Map color");
        bool active = _settings.Gameplay.Planets.Enabled && CurrentPlanet.Enabled;
        source.interactable = colorButton.interactable = active;
      });
    }
    _ui.Bind(() =>
    {
      _planetProfileEnabled.interactable = _settings.Gameplay.Planets.Enabled;
      RefreshPlanetPreview();
    });
  }

  private void RefreshPlanetPreview()
  {
    if (_page != 6 || !IsOpen || _planetImage == null)
      return;
    _nextPlanetPreview = Time.unscaledTime + .1f;
    Texture texture = _renderPlanetPreview?.Invoke(_planetIndex, CurrentPlanet);
    _planetImage.texture = texture;
    _planetImage.enabled = texture != null;
    LayoutPlanetArtwork();
    _planetPreviewLabel.text =
      texture == null ? L("공 미리보기를 불러올 수 없습니다.", "Planet preview is unavailable.")
      : !_settings.Gameplay.Planets.Enabled || !CurrentPlanet.Enabled
        ? L("미리보기 · 게임에는 적용 안 됨", "Preview · not applied")
      : L("색상 미리보기", "Color preview");
  }

  private void LayoutPlanetArtwork()
  {
    float availableWidth = Mathf.Max(1, _planetPreview.rect.width - 32);
    float availableHeight = Mathf.Max(1, _planetPreview.rect.height - 64);
    Texture texture = _planetImage.texture;
    float aspect = texture != null && texture.height > 0 ? texture.width / (float)texture.height : 1;
    float width = Mathf.Min(availableWidth, availableHeight * aspect);
    float height = width / aspect;
    SettingsUiFactory.Place(
      _planetImage.rectTransform,
      (_planetPreview.rect.width - width) / 2,
      12 + (availableHeight - height) / 2,
      width,
      height
    );
  }

  private static Color ParsePlanetColor(PlanetComponentAppearance appearance)
  {
    if (!OverlayTheme.TryParseHighlight(appearance.ColorHex, out Color color))
      color = Color.white;
    color.a = appearance.Opacity / 100;
    return color;
  }

  private void CreatePlanetPopover(TMP_FontAsset font)
  {
    _pickerShade = _ui.Button(
      "",
      _window,
      0,
      0,
      1000,
      600,
      () =>
      {
        if (CommitPopover())
          HidePopover();
      },
      false,
      false
    );
    _pickerShade.gameObject.name = "Dismiss color picker";
    _pickerShade.GetComponent<SettingsCursor>().enabled = false;
    _pickerShade.targetGraphic.color = new Color(0, 0, 0, .18f);
    _pickerShade.transition = Selectable.Transition.None;
    _pickerShade.GetComponentInChildren<TextMeshProUGUI>().gameObject.SetActive(false);
    _pickerShade.gameObject.SetActive(false);
    _planetPicker = new SettingsColorPicker(_window, font, () => _settings.ReduceUiMotion);
    _planetPicker.Hidden = () => _pickerShade.gameObject.SetActive(false);
  }

  private void OpenPlanetColor(int index)
  {
    PlanetComponentAppearance part = PlanetPart(index);
    string title =
      index == 0 ? L("공 색상", "Body color")
      : index == 1 ? L("꼬리 색상", "Trail color")
      : L("고리 색상", "Ring color");
    _pickerAnchor = (RectTransform)_planetColorButtons[index].transform;
    _pickerShade.gameObject.SetActive(true);
    _pickerShade.transform.SetAsLastSibling();
    _planetPicker.Show(
      title,
      ParsePlanetColor(part),
      true,
      value =>
      {
        string hex = "#" + ColorUtility.ToHtmlStringRGB(value);
        if (part.ColorHex != hex)
          part.OverrideColor = true;
        part.ColorHex = hex;
        part.Opacity = Mathf.Round(value.a * 100);
        Changed();
      }
    );
    LayoutPopover();
  }

  private bool CommitPopover() => _planetPicker == null || !_planetPicker.Visible || _planetPicker.Commit();

  private void HidePopover()
  {
    _planetPicker?.Hide();
    if (_pickerShade != null)
      _pickerShade.gameObject.SetActive(false);
  }

  private bool DismissPopover()
  {
    if (_planetPicker?.Visible != true)
      return false;
    HidePopover();
    return true;
  }

  private void LayoutPopover()
  {
    if (_pickerShade == null)
      return;
    SettingsUiFactory.Place((RectTransform)_pickerShade.transform, 0, 0, _window.rect.width, _window.rect.height);
    if (_planetPicker?.Visible != true)
      return;
    Vector3 local = _window.InverseTransformPoint(_pickerAnchor.position);
    float x = local.x - _window.rect.xMin;
    float y = _window.rect.yMax - local.y + _pickerAnchor.rect.height + 8;
    float scale = Mathf.Min(
      1,
      (_window.rect.width - 20) / SettingsColorPicker.Width,
      (_window.rect.height - 20) / _planetPicker.Height
    );
    _planetPicker.Root.localScale = Vector3.one * scale;
    x = Mathf.Clamp(x, 10, _window.rect.width - SettingsColorPicker.Width * scale - 10);
    y = Mathf.Clamp(y, 10, _window.rect.height - _planetPicker.Height * scale - 10);
    SettingsUiFactory.Place(_planetPicker.Root, x, y, SettingsColorPicker.Width, _planetPicker.Height);
    _planetPicker.Root.SetAsLastSibling();
  }

  private void LayoutPlanetPage(float width)
  {
    bool wide = width >= 600;
    float headerHeight = width < 480 ? 116 : 88;
    SettingsUiFactory.Place((RectTransform)_planetEnabled.transform, 0, 0, width, headerHeight);
    LayoutTabs(_planetTabs, _planetTabButtons, width, headerHeight + 12);
    float top = headerHeight + 68;
    float previewWidth = wide ? (width >= 760 ? 268 : 200) : width;
    float previewHeight = wide ? 268 : 168;
    SettingsUiFactory.Place(_planetPreview, 0, top, previewWidth, previewHeight);
    LayoutPlanetArtwork();
    SettingsUiFactory.Place(_planetPreviewLabel.rectTransform, 12, previewHeight - 44, previewWidth - 24, 28);
    float controlsWidth = wide ? width - previewWidth - 16 : width;
    float controlsTop = wide ? top : top + previewHeight + 12;
    float rowHeight = controlsWidth < 400 ? 100 : 60;
    SettingsUiFactory.Place(
      _planetComponents,
      wide ? previewWidth + 16 : 0,
      controlsTop,
      controlsWidth,
      64 + 3 * (rowHeight + 8)
    );
    SettingsUiFactory.Place((RectTransform)_planetProfileEnabled.transform, 0, 0, controlsWidth, 56);
    for (int i = 0; i < _planetRows.Count; i++)
    {
      SettingsUiFactory.Place(_planetRows[i], 0, 64 + i * (rowHeight + 8), controlsWidth, rowHeight);
      bool compact = controlsWidth < 400;
      SettingsUiFactory.Place(
        (RectTransform)_planetSourceButtons[i].transform,
        compact ? 16 : controlsWidth - 270,
        compact ? 50 : 10,
        130,
        40
      );
      SettingsUiFactory.Place(
        (RectTransform)_planetColorButtons[i].transform,
        controlsWidth - 124,
        compact ? 50 : 10,
        108,
        40
      );
    }
    ((RectTransform)_pages[6].transform).sizeDelta = new Vector2(width, controlsTop + _planetComponents.rect.height);
  }
}
