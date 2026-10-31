using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static Element.Presentation.Settings.SettingsLocale;

namespace Element.Presentation.Settings;

// One retained picker serves the shared overlay highlight and independent RGBA
// planet colors. It owns gesture state; consumers decide what the color affects.
internal sealed class SettingsColorPicker : IDisposable
{
  public const float Width = 288;
  private readonly SettingsUiFactory _ui;
  private readonly TextMeshProUGUI _title,
    _error;
  private readonly SettingsColorPlane _plane;
  private readonly Slider _hue,
    _alpha;
  private readonly TMP_InputField _hex,
    _opacity;
  private readonly Image _swatch;
  private readonly SettingsCapsuleGraphic _markerColor,
    _hueMarker;
  private readonly SettingsAlphaGraphic _alphaGraphic;
  private readonly Button _reset;
  private Action<Color> _changed;
  private Color _value,
    _defaultColor;
  private float _h,
    _s,
    _v;
  private bool _syncing,
    _allowOpacity,
    _hasDefault;
  public RectTransform Root { get; }
  public float Height => _allowOpacity ? 358 : 318;
  public bool Visible => Root.gameObject.activeSelf;
  public bool IsTyping => Visible && (_hex.isFocused || _opacity.isFocused);
  public Action Hidden;

  public SettingsColorPicker(Transform parent, TMP_FontAsset font, Func<bool> reduceMotion)
  {
    _ui = new SettingsUiFactory(font) { ReduceMotion = reduceMotion };
    Image surface = _ui.Panel("Color picker", parent, 0, 0, Width, Height, new Color(.105f, .105f, .105f));
    surface.raycastTarget = true;
    Root = surface.rectTransform;
    _title = _ui.Text("", Root, 16, 10, 208, 32, 16);
    _ui.IconButton("Close color picker", Root, 236, 6, 40, Hide, 5);

    SettingsColorMask plane = _ui.Rect("Saturation and brightness", Root, 16, 52, 256, 156)
      .gameObject.AddComponent<SettingsColorMask>();
    plane.color = Color.white;
    plane.raycastTarget = true;
    plane.gameObject.AddComponent<Mask>().showMaskGraphic = true;
    _plane = plane.gameObject.AddComponent<SettingsColorPlane>();
    _plane.navigation = new Navigation { mode = Navigation.Mode.None };
    _plane.transition = Selectable.Transition.None;
    _plane.targetGraphic = plane;
    SettingsCursor.Attach(plane.gameObject, SettingsCursorKind.Crosshair);
    RectTransform gradient = _ui.Rect("HSV gradient", plane.transform, 0, 0, 256, 156);
    SettingsUiFactory.Stretch(gradient, 0, 0, 0, 0);
    _plane.Gradient = gradient.gameObject.AddComponent<SettingsGradientGraphic>();
    _plane.Gradient.raycastTarget = false;
    _plane.Marker = _ui.Capsule("Color position", plane.transform, 0, 0, 18, 18, Color.white).rectTransform;
    _plane.Marker.pivot = new Vector2(.5f, .5f);
    _markerColor = _ui.Capsule("Selected color", _plane.Marker, 3, 3, 12, 12, Color.white);
    _plane.Changed = (s, v) =>
    {
      _s = s;
      _v = v;
      ApplyHsv();
    };

    _hue = _ui.Slider(
      "Hue",
      Root,
      6,
      214,
      276,
      0,
      h =>
      {
        if (_syncing)
          return;
        _h = h;
        ApplyHsv();
      },
      1f / 360
    );
    _hue.fillRect.gameObject.SetActive(false);
    _hue.transform.Find("Track").gameObject.SetActive(false);
    SettingsCapsuleGraphic spectrumMask = _ui.Capsule("Hue spectrum", _hue.transform, 10, 12, 256, 12, Color.white);
    spectrumMask.transform.SetSiblingIndex(2);
    spectrumMask.gameObject.AddComponent<Mask>().showMaskGraphic = true;
    RectTransform spectrum = _ui.Rect("Spectrum", spectrumMask.transform, 0, 0, 256, 12);
    SettingsGradientGraphic hueGraphic = spectrum.gameObject.AddComponent<SettingsGradientGraphic>();
    hueGraphic.HueStrip = true;
    hueGraphic.raycastTarget = false;
    _hueMarker = _ui.Capsule("Hue marker center", _hue.handleRect, 4, 4, 12, 12, Color.white);

    _alpha = _ui.Slider(
      "Opacity",
      Root,
      6,
      250,
      276,
      1,
      a =>
      {
        if (_syncing)
          return;
        Color color = _value;
        color.a = a;
        Apply(color, true, true);
      },
      .01f
    );
    _alpha.fillRect.gameObject.SetActive(false);
    _alpha.transform.Find("Track").gameObject.SetActive(false);
    SettingsCapsuleGraphic opacityMask = _ui.Capsule("Opacity track", _alpha.transform, 10, 12, 256, 12, Color.white);
    opacityMask.transform.SetSiblingIndex(2);
    opacityMask.gameObject.AddComponent<Mask>().showMaskGraphic = true;
    RectTransform alphaGradient = _ui.Rect("Alpha checkerboard", opacityMask.transform, 0, 0, 256, 12);
    _alphaGraphic = alphaGradient.gameObject.AddComponent<SettingsAlphaGraphic>();
    _alphaGraphic.raycastTarget = false;

    _hex = _ui.Input("HEX color", "", Root, 16, 254, 176, HexChanged, _ => Commit(), 7);
    SettingsUiFactory.Stretch(_hex.textViewport, 38, 4, 8, 4);
    _hex.textComponent.textWrappingMode = TextWrappingModes.NoWrap;
    _swatch = _ui.Panel("Current color", _hex.transform, 12, 12, 16, 16, Color.white);
    _opacity = _ui.Input("Opacity percentage", "100", Root, 200, 294, 72, OpacityChanged, _ => Commit(), 3);
    _opacity.contentType = TMP_InputField.ContentType.IntegerNumber;
    SettingsUiFactory.Stretch(_opacity.textViewport, 10, 4, 23, 4);
    _opacity.textComponent.alignment = TextAlignmentOptions.Right;
    _opacity.textComponent.textWrappingMode = TextWrappingModes.NoWrap;
    _ui.Text("%", _opacity.transform, 49, 0, 17, 40, 13, SettingsUiFactory.Muted);
    _reset = _ui.Button(L("기본값", "Default"), Root, 200, 254, 72, 40, ResetColor);
    TextMeshProUGUI resetText = _reset.GetComponentInChildren<TextMeshProUGUI>();
    SettingsUiFactory.Stretch(resetText.rectTransform, 6, 0, 6, 0);
    resetText.fontSize = 14;
    resetText.textWrappingMode = TextWrappingModes.NoWrap;
    _error = _ui.Text("", Root, 16, 297, 256, 16, 11, new Color(1, .66f, .53f));
    Root.gameObject.SetActive(false);
  }

  public void SetDefault(Color color)
  {
    _hasDefault = true;
    _defaultColor = color;
  }

  public void Show(string title, Color value, bool opacity, Action<Color> changed)
  {
    _changed = changed;
    _allowOpacity = opacity;
    _title.text = title;
    _alpha.gameObject.SetActive(opacity);
    _opacity.gameObject.SetActive(opacity);
    _reset.gameObject.SetActive(!opacity && _hasDefault);
    float bottom = opacity ? 294 : 254;
    float hexWidth = opacity || _hasDefault ? 176 : 256;
    SettingsUiFactory.Place((RectTransform)_hex.transform, 16, bottom, hexWidth, 40);
    SettingsUiFactory.Place(_error.rectTransform, 16, bottom + 43, 256, 16);
    Root.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Height);
    if (!opacity)
      value.a = 1;
    Color.RGBToHSV(value, out _h, out _s, out _v);
    Apply(value, true, false);
    Root.gameObject.SetActive(true);
    Root.SetAsLastSibling();
  }

  public void Hide()
  {
    if (!Visible)
      return;
    Root.gameObject.SetActive(false);
    UIInteractionState.SetTextEditing(false);
    Hidden?.Invoke();
  }

  public bool Commit()
  {
    if (!Visible)
      return true;
    if (!TryReadHex(_hex.text, out Color color))
    {
      _error.text = L("BA712B처럼 6자리로 입력하세요", "Use six hex digits, e.g. BA712B");
      Focus(_hex);
      return false;
    }
    color.a = _value.a;
    if (_allowOpacity)
    {
      if (!TryReadOpacity(_opacity.text, out float opacity))
      {
        _error.text = L("불투명도를 0–100%로 입력하세요", "Enter opacity from 0 to 100%");
        Focus(_opacity);
        return false;
      }
      color.a = opacity;
    }
    Apply(color, true, true);
    return true;
  }

  private static void Focus(TMP_InputField input)
  {
    input.Select();
    input.ActivateInputField();
  }

  private void HexChanged(string value)
  {
    if (_syncing)
      return;
    _error.text = "";
    if (!TryReadHex(value, out Color color))
      return;
    color.a = _value.a;
    Color.RGBToHSV(color, out _h, out _s, out _v);
    Apply(color, false, true);
  }

  private void OpacityChanged(string text)
  {
    if (_syncing)
      return;
    _error.text = "";
    if (!TryReadOpacity(text, out float alpha))
      return;
    Color color = _value;
    color.a = alpha;
    Apply(color, false, true);
  }

  private static bool TryReadHex(string text, out Color color)
  {
    color = Color.white;
    if (string.IsNullOrWhiteSpace(text))
      return false;
    string hex = text.Trim();
    if (hex.StartsWith("#", StringComparison.Ordinal))
      hex = hex.Substring(1);
    if (hex.Length != 6)
      return false;
    for (int i = 0; i < hex.Length; i++)
      if (!Uri.IsHexDigit(hex[i]))
        return false;
    return ColorUtility.TryParseHtmlString("#" + hex, out color);
  }

  private static bool TryReadOpacity(string text, out float opacity)
  {
    bool valid =
      int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int percent)
      && percent >= 0
      && percent <= 100;
    opacity = percent / 100f;
    return valid;
  }

  private void ResetColor()
  {
    Color.RGBToHSV(_defaultColor, out _h, out _s, out _v);
    Apply(_defaultColor, true, true);
  }

  private void ApplyHsv()
  {
    Color color = Color.HSVToRGB(_h, _s, _v);
    color.a = _value.a;
    Apply(color, true, true);
  }

  private void Apply(Color color, bool updateText, bool notify)
  {
    if (!_allowOpacity)
      color.a = 1;
    _value = color;
    _syncing = true;
    _plane.Set(_h, _s, _v);
    _hue.SetValueWithoutNotify(_h);
    _alpha.SetValueWithoutNotify(color.a);
    if (updateText)
    {
      _hex.SetTextWithoutNotify(ColorUtility.ToHtmlStringRGB(color));
      _opacity.SetTextWithoutNotify(Mathf.RoundToInt(color.a * 100).ToString(CultureInfo.InvariantCulture));
    }
    Color opaque = color;
    opaque.a = 1;
    _swatch.color = opaque;
    _markerColor.color = opaque;
    _hueMarker.color = Color.HSVToRGB(_h, 1, 1);
    _alphaGraphic.SetColor(opaque);
    _error.text = "";
    if (notify)
      _changed?.Invoke(color);
    _ui.RefreshTheme();
    _syncing = false;
  }

  public void Dispose()
  {
    _ui.Dispose();
    SettingsUiFactory.Destroy(Root.gameObject);
  }
}

// A geometric stencil avoids magnifying the few corner texels in the shared
// small nine-slice sprite across the large saturation/value surface.
[RequireComponent(typeof(CanvasRenderer))]
internal sealed class SettingsColorMask : MaskableGraphic
{
  protected override void OnPopulateMesh(VertexHelper mesh)
  {
    mesh.Clear();
    Rect r = rectTransform.rect;
    float radius = Mathf.Min(12, Mathf.Min(r.width, r.height) * .5f);
    mesh.AddVert(r.center, color, Vector2.zero);
    const int segments = 12;
    int count = 0;
    for (int corner = 0; corner < 4; corner++)
    {
      Vector2 center = new(
        corner == 0 || corner == 3 ? r.xMax - radius : r.xMin + radius,
        corner < 2 ? r.yMax - radius : r.yMin + radius
      );
      for (int step = 0; step <= segments; step++)
      {
        float angle = (corner + step / (float)segments) * Mathf.PI * .5f;
        mesh.AddVert(center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius, color, Vector2.zero);
        if (count > 0)
          mesh.AddTriangle(0, count, count + 1);
        count++;
      }
    }
    mesh.AddTriangle(0, count, 1);
  }
}

[RequireComponent(typeof(CanvasRenderer))]
internal sealed class SettingsAlphaGraphic : MaskableGraphic
{
  private Color _value;

  public void SetColor(Color color)
  {
    if (_value == color)
      return;
    _value = color;
    SetVerticesDirty();
  }

  protected override void OnPopulateMesh(VertexHelper mesh)
  {
    mesh.Clear();
    Rect rect = rectTransform.rect;
    const float cell = 6;
    int columns = Mathf.CeilToInt(rect.width / cell),
      rows = Mathf.CeilToInt(rect.height / cell);
    for (int y = 0; y < rows; y++)
    for (int x = 0; x < columns; x++)
    {
      float left = x * cell,
        right = Mathf.Min(rect.width, left + cell);
      float bottom = y * cell,
        top = Mathf.Min(rect.height, bottom + cell);
      Color checker = (x + y) % 2 == 0 ? new Color(.30f, .30f, .30f) : new Color(.48f, .48f, .48f);
      Color startColor = Color.Lerp(checker, _value, left / rect.width);
      Color endColor = Color.Lerp(checker, _value, right / rect.width);
      int start = mesh.currentVertCount;
      mesh.AddVert(new Vector2(rect.xMin + left, rect.yMin + bottom), startColor, Vector2.zero);
      mesh.AddVert(new Vector2(rect.xMin + left, rect.yMin + top), startColor, Vector2.zero);
      mesh.AddVert(new Vector2(rect.xMin + right, rect.yMin + top), endColor, Vector2.zero);
      mesh.AddVert(new Vector2(rect.xMin + right, rect.yMin + bottom), endColor, Vector2.zero);
      mesh.AddTriangle(start, start + 1, start + 2);
      mesh.AddTriangle(start, start + 2, start + 3);
    }
  }
}
