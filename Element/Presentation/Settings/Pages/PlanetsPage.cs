using System;
using System.Collections.Generic;
using Element.Configuration.Gameplay;
using Element.Presentation.Ui.Theme;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static Element.Presentation.Ui.Theme.UiLocale;

namespace Element.Presentation.Settings.Pages;

/// <summary>Tweaks → Planets: each planet's body, trail and ring colors, with the game's own preview.</summary>
internal sealed class PlanetsPage : SettingsPage
{
  private readonly Button _planetEnabled,
    _planetProfileEnabled;
  private readonly SettingsSegmentedControl _planetTabs;
  private readonly RectTransform _planetPreview,
    _planetComponents;
  private readonly List<Button> _planetColorButtons = new();
  private readonly List<Button> _planetSourceButtons = new();
  private readonly List<RectTransform> _planetRows = new();
  private readonly Func<int, PlanetAppearanceProfile, Texture> _renderPlanetPreview;
  private readonly RawImage _planetImage;
  private readonly TextMeshProUGUI _planetPreviewLabel;
  private readonly SettingsColorPicker _planetPicker;
  private float _nextPlanetPreview;
  private int _planetIndex;
  private RectTransform _pickerAnchor;

  /// <param name="renderPreview">Draws planet <c>index</c> as the game would; null shows that the preview is unavailable.</param>
  public PlanetsPage(ISettingsWindow window, Func<int, PlanetAppearanceProfile, Texture> renderPreview)
    : base(window, "Planets")
  {
    _renderPlanetPreview = renderPreview;
    _planetEnabled = Ui.Switch(
      L("행성 꾸미기", "Custom planets"),
      L("행성별 색상과 불투명도를 직접 설정합니다.", "Set color and opacity for each planet."),
      Root,
      0,
      () => Settings.Gameplay.Planets.Enabled,
      value =>
      {
        Settings.Gameplay.Planets.Enabled = value;
        Window.Changed();
      }
    );
    _planetTabs = Tabs(
      Root,
      new[] { L("불", "Fire"), L("얼음", "Ice"), L("세 번째 공", "Third planet") },
      value =>
      {
        Window.HidePopovers();
        _planetIndex = value;
      }
    );
    _planetPreview = Ui.Panel("Planet preview", Root, 0, 156, 300, 248, UiPalette.Surface).rectTransform;
    RectTransform artwork = Ui.Rect("Game planet preview", _planetPreview, 16, 16, 268, 168);
    _planetImage = artwork.gameObject.AddComponent<RawImage>();
    _planetImage.raycastTarget = false;
    _planetImage.enabled = false;
    _planetPreviewLabel = Ui.Text(
      L("색상 미리보기", "Color preview"),
      _planetPreview,
      16,
      198,
      268,
      28,
      13,
      UiPalette.Muted
    );
    _planetPreviewLabel.alignment = TextAlignmentOptions.Midline;
    _planetComponents = Ui.Rect("Planet components", Root, 316, 156, 620, 248);
    _planetProfileEnabled = Ui.Switch(
      L("이 행성에 적용", "Customize this planet"),
      "",
      _planetComponents,
      0,
      () => CurrentPlanet.Enabled,
      value =>
      {
        CurrentPlanet.Enabled = value;
        Window.Changed();
      }
    );
    // A compact single-line switch does not reserve a second empty copy line.
    TextMeshProUGUI[] profileLabels = _planetProfileEnabled.GetComponentsInChildren<TextMeshProUGUI>();
    profileLabels[1].gameObject.SetActive(false);
    string[] names = { L("공", "Body"), L("꼬리", "Trail"), L("고리", "Ring") };
    for (int i = 0; i < names.Length; i++)
      CreatePartRow(i, names[i]);
    Ui.Bind(() =>
    {
      _planetProfileEnabled.interactable = Settings.Gameplay.Planets.Enabled;
      RefreshPlanetPreview();
    });
    _planetPicker = new SettingsColorPicker(Window.Frame, Window.Font, () => Settings.ReduceUiMotion);
    _planetPicker.Hidden = Window.Shade.Hide;
  }

  public override string Title => L("행성", "Planets");

  private PlanetAppearanceProfile CurrentPlanet =>
    _planetIndex == 0 ? Settings.Gameplay.Planets.Red
    : _planetIndex == 1 ? Settings.Gameplay.Planets.Blue
    : Settings.Gameplay.Planets.Green;

  private PlanetComponentAppearance PlanetPart(int index) =>
    index == 0 ? CurrentPlanet.Body
    : index == 1 ? CurrentPlanet.Tail
    : CurrentPlanet.Ring;

  private void CreatePartRow(int part, string name)
  {
    var row = Ui.Panel(name, _planetComponents, 0, 72 + part * 68, 620, 60, UiPalette.Surface).rectTransform;
    _planetRows.Add(row);
    Ui.Text(name, row, 16, 10, 112, 40, 16);
    Button source = Ui.Button(
      "",
      row,
      132,
      10,
      120,
      40,
      () =>
      {
        PlanetPart(part).OverrideColor = !PlanetPart(part).OverrideColor;
        Window.Changed();
      }
    );
    source.gameObject.name = name + " color source";
    _planetSourceButtons.Add(source);
    Button colorButton = Ui.Button("", row, 264, 10, 180, 40, () => OpenPlanetColor(part));
    colorButton.gameObject.name = "Edit " + name + " color and opacity";
    _planetColorButtons.Add(colorButton);
    TextMeshProUGUI value = colorButton.GetComponentInChildren<TextMeshProUGUI>();
    SettingsUiFactory.Stretch(value.rectTransform, 40, 0, 10, 0);
    value.fontSize = 14;
    var swatch = Ui.Panel("Color swatch", colorButton.transform, 12, 12, 16, 16, Color.white);
    Ui.Bind(() =>
    {
      var appearance = PlanetPart(part);
      Color parsed = ParsePlanetColor(appearance);
      swatch.color = new Color(parsed.r, parsed.g, parsed.b, 1);
      value.text = appearance.Opacity.ToString("0") + "%";
      source.GetComponentInChildren<TextMeshProUGUI>().text = appearance.OverrideColor
        ? L("직접 지정", "Custom color")
        : L("맵 색상", "Map color");
      bool active = Settings.Gameplay.Planets.Enabled && CurrentPlanet.Enabled;
      source.interactable = colorButton.interactable = active;
    });
  }

  public override void Shown() => RefreshPlanetPreview();

  public override void Tick()
  {
    if (Time.unscaledTime >= _nextPlanetPreview)
      RefreshPlanetPreview();
  }

  private void RefreshPlanetPreview()
  {
    if (!Window.IsShowing(this))
      return;
    _nextPlanetPreview = Time.unscaledTime + .1f;
    Texture texture = _renderPlanetPreview?.Invoke(_planetIndex, CurrentPlanet);
    _planetImage.texture = texture;
    _planetImage.enabled = texture != null;
    LayoutPlanetArtwork();
    _planetPreviewLabel.text =
      texture == null ? L("공 미리보기를 불러올 수 없습니다.", "Planet preview is unavailable.")
      : !Settings.Gameplay.Planets.Enabled || !CurrentPlanet.Enabled
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
    if (!UiTheme.TryParseHighlight(appearance.ColorHex, out Color color))
      color = Color.white;
    color.a = appearance.Opacity / 100;
    return color;
  }

  private void OpenPlanetColor(int index)
  {
    PlanetComponentAppearance part = PlanetPart(index);
    string title =
      index == 0 ? L("공 색상", "Body color")
      : index == 1 ? L("꼬리 색상", "Trail color")
      : L("고리 색상", "Ring color");
    _pickerAnchor = (RectTransform)_planetColorButtons[index].transform;
    Window.Shade.Show();
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
        Window.Changed();
      }
    );
    LayoutPopover();
  }

  public override bool CommitPopovers() => !_planetPicker.Visible || _planetPicker.Commit();

  public override void HidePopovers() => _planetPicker.Hide();

  public override bool HandleEscape()
  {
    if (!_planetPicker.Visible)
      return false;
    Window.HidePopovers();
    return true;
  }

  public override GameObject FocusScope => _planetPicker.Visible ? _planetPicker.Root.gameObject : null;

  public override void Dispose() => _planetPicker.Dispose();

  private void LayoutPopover()
  {
    Window.Shade.Layout();
    if (!_planetPicker.Visible)
      return;
    RectTransform window = Window.Frame;
    Vector3 local = window.InverseTransformPoint(_pickerAnchor.position);
    float x = local.x - window.rect.xMin;
    float y = window.rect.yMax - local.y + _pickerAnchor.rect.height + 8;
    float scale = Mathf.Min(
      1,
      (window.rect.width - 20) / SettingsColorPicker.Width,
      (window.rect.height - 20) / _planetPicker.Height
    );
    _planetPicker.Root.localScale = Vector3.one * scale;
    x = Mathf.Clamp(x, 10, window.rect.width - SettingsColorPicker.Width * scale - 10);
    y = Mathf.Clamp(y, 10, window.rect.height - _planetPicker.Height * scale - 10);
    SettingsUiFactory.Place(_planetPicker.Root, x, y, SettingsColorPicker.Width, _planetPicker.Height);
    _planetPicker.Root.SetAsLastSibling();
  }

  public override void Layout(float width, float height)
  {
    bool wide = width >= 600;
    float headerHeight = width < 480 ? 116 : 88;
    SettingsUiFactory.Place((RectTransform)_planetEnabled.transform, 0, 0, width, headerHeight);
    _planetTabs.Place(0, headerHeight + 12, width, 44);
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
    Root.sizeDelta = new Vector2(width, controlsTop + _planetComponents.rect.height);
    LayoutPopover();
  }
}
