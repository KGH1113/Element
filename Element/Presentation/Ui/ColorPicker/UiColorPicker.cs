using System;
using System.Globalization;
using Element.Application.Input;
using Element.Configuration.Overlay;
using Element.Domain.Overlay.Gradient;
using Element.Presentation.Ui.Graphics;
using Element.Presentation.Ui.Input;
using Element.Presentation.Ui.Theme;
using Element.Presentation.Ui.Widgets;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static Element.Presentation.Ui.Theme.UiLocale;

namespace Element.Presentation.Ui.ColorPicker;

// One retained picker serves the shared overlay highlight and independent RGBA
// planet colors. It owns gesture state; consumers decide what the color affects.
internal sealed class UiColorPicker : IDisposable, IGradientBarHost
{
  public const float Width = 288;
  private readonly UiFactory _ui;
  private readonly TextMeshProUGUI _title,
    _error;
  private readonly UiColorPlane _plane;
  private readonly Slider _hue,
    _alpha;
  private readonly TMP_InputField _hex,
    _opacity;
  private readonly Image _swatch;
  private readonly UiCapsuleGraphic _markerColor,
    _hueMarker;
  private readonly UiAlphaGraphic _alphaGraphic;
  private readonly Button _reset;
  private readonly Button _close;
  private readonly Image _surface;
  private bool _column;
  private float _columnWidth;
  private Action<Color> _changed;
  private Color _value,
    _defaultColor;
  private float _h,
    _s,
    _v;
  private bool _syncing,
    _allowOpacity,
    _hasDefault,
    _sourceMode,
    _followsHighlight;
  private readonly RectTransform _planeRect;
  private readonly UiSegmentedControl _source;
  private readonly TextMeshProUGUI _opacityLabel;
  private readonly RectTransform _opacitySlot;
  private bool _externalOpacity;
  private Action<OverlayColorSettings> _sourceChanged;
  private const float SourceRow = 44;

  // An editor's picker is two columns: the color bar with its pill above it,
  // the kinds and Highlight/Custom on the left; the color's own controls on
  // the right. The bar keeps its place whether the color is solid or not.
  private const float ColumnWidth = 256,
    ColumnGap = 20,
    TwoColumnWidth = ColumnWidth * 2 + ColumnGap,
    RightColumn = ColumnWidth + ColumnGap;

  // An editor's picker keeps one height, that of the custom color's column,
  // so switching Highlight/Custom or adding a color moves nothing; following
  // the highlight leaves room under its opacity.
  private const float TwoColumnHeight = 290;

  public RectTransform Root { get; }

  /// <summary>The color bar of an overlay editor's picker: its stops, kinds and the picked stop.</summary>
  public UiGradientBar Gradient { get; }

  /// <summary>The color bar shows: in an overlay editor's picker.</summary>
  public bool GradientAvailable => _sourceMode && _column;

  private float ColumnHeight =>
    GradientAvailable ? TwoColumnHeight
    : _followsHighlight ? 142
    : 334;
  public float Height =>
    _column ? ColumnHeight
    : !_sourceMode ? (_allowOpacity ? 358 : 318)
    : _followsHighlight ? 206
    : 358 + SourceRow;

  /// <summary>The width as laid out now.</summary>
  public float CurrentWidth => _column ? (GradientAvailable ? TwoColumnWidth : _columnWidth) : Width;

  public bool Column => _column;

  /// <summary>
  /// Lays the picker out borderless for the panel beside it, such as the
  /// keypad below it: no card, title or close. An overlay editor's picker
  /// (<see cref="ShowSource"/>) takes two columns, the color bar and
  /// Highlight/Custom on the left and the color square, sliders and HEX with
  /// opacity on the right, so it stays short; others are one column
  /// <paramref name="width"/> wide. Whoever places it names the color and
  /// closes it.
  /// </summary>
  public void LayOutAsColumn(float width)
  {
    _column = true;
    _columnWidth = width;
    _surface.color = Color.clear;
    _title.gameObject.SetActive(false);
    _close.gameObject.SetActive(false);
    Layout();
  }

  public bool Visible => Root.gameObject.activeSelf;
  public bool IsTyping => Visible && (_hex.isFocused || _opacity.isFocused);
  public Action Hidden;

  /// <summary>
  /// Where the opacity box sits. With <see cref="ExternalOpacity"/> the owner
  /// puts its own opacity control here and the typed box is hidden.
  /// </summary>
  public RectTransform OpacitySlot => _opacitySlot;

  /// <summary>The owner shows and edits the opacity in <see cref="OpacitySlot"/>.</summary>
  public bool ExternalOpacity
  {
    get => _externalOpacity;
    set
    {
      if (_externalOpacity == value)
        return;
      _externalOpacity = value;
      Layout();
    }
  }

  public UiColorPicker(Transform parent, TMP_FontAsset font, Func<bool> reduceMotion)
  {
    _ui = new UiFactory(font) { ReduceMotion = reduceMotion };
    Image surface = _ui.Panel("Color picker", parent, 0, 0, Width, Height, new Color(.105f, .105f, .105f));
    surface.raycastTarget = true;
    _surface = surface;
    Root = surface.rectTransform;
    _title = _ui.Text("", Root, 16, 10, 208, 32, 16);
    _close = _ui.IconButton("Close color picker", Root, 236, 6, 40, Hide, UiIconKind.WindowClose);
    _source = new UiSegmentedControl(
      _ui,
      Root,
      16,
      52,
      256,
      36,
      new[] { L("하이라이트 색", "Highlight Color"), L("직접 고르기", "Custom Color") },
      index => SetSource(index == 0),
      13
    );

    UiColorMask plane = _ui.Rect("Saturation and brightness", Root, 16, 52, 256, 156)
      .gameObject.AddComponent<UiColorMask>();
    _planeRect = plane.rectTransform;
    plane.color = Color.white;
    plane.raycastTarget = true;
    plane.gameObject.AddComponent<Mask>().showMaskGraphic = true;
    _plane = plane.gameObject.AddComponent<UiColorPlane>();
    _plane.navigation = new Navigation { mode = Navigation.Mode.None };
    _plane.transition = Selectable.Transition.None;
    _plane.targetGraphic = plane;
    UiCursor.Attach(plane.gameObject, UiCursorKind.Crosshair);
    RectTransform gradient = _ui.Rect("HSV gradient", plane.transform, 0, 0, 256, 156);
    UiFactory.Stretch(gradient, 0, 0, 0, 0);
    _plane.Gradient = gradient.gameObject.AddComponent<UiGradientGraphic>();
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
    UiCapsuleGraphic spectrumMask = _ui.Capsule("Hue spectrum", _hue.transform, 10, 12, 256, 12, Color.white);
    spectrumMask.transform.SetSiblingIndex(2);
    spectrumMask.gameObject.AddComponent<Mask>().showMaskGraphic = true;
    RectTransform spectrum = _ui.Rect("Spectrum", spectrumMask.transform, 0, 0, 256, 12);
    UiGradientGraphic hueGraphic = spectrum.gameObject.AddComponent<UiGradientGraphic>();
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
    UiCapsuleGraphic opacityMask = _ui.Capsule("Opacity track", _alpha.transform, 10, 12, 256, 12, Color.white);
    opacityMask.transform.SetSiblingIndex(2);
    opacityMask.gameObject.AddComponent<Mask>().showMaskGraphic = true;
    RectTransform alphaGradient = _ui.Rect("Alpha checkerboard", opacityMask.transform, 0, 0, 256, 12);
    _alphaGraphic = alphaGradient.gameObject.AddComponent<UiAlphaGraphic>();
    _alphaGraphic.raycastTarget = false;

    _hex = _ui.Input("HEX color", "", Root, 16, 254, 176, HexChanged, _ => Commit(), 7);
    UiFactory.Stretch(_hex.textViewport, 38, 4, 8, 4);
    _hex.textComponent.textWrappingMode = TextWrappingModes.NoWrap;
    _swatch = _ui.Panel("Current color", _hex.transform, 12, 12, 16, 16, Color.white);
    _opacity = _ui.Input("Opacity percentage", "100", Root, 200, 294, 72, OpacityChanged, _ => Commit(), 3);
    _opacity.contentType = TMP_InputField.ContentType.IntegerNumber;
    UiFactory.Stretch(_opacity.textViewport, 10, 4, 23, 4);
    _opacity.textComponent.alignment = TextAlignmentOptions.Right;
    // Right-aligned text puts the caret on the field's edge; keep room for it.
    _opacity.textComponent.margin = new Vector4(0, 0, 2, 0);
    _opacity.textComponent.textWrappingMode = TextWrappingModes.NoWrap;
    _ui.Text("%", _opacity.transform, 49, 0, 17, 40, 13, UiPalette.Muted);
    _opacityLabel = _ui.Text(L("불투명도", "Opacity"), Root, 16, 140, 176, 40, 14, UiPalette.Muted);
    _opacitySlot = _ui.Rect("Opacity slot", Root, 200, 294, 72, 40);
    _opacitySlot.gameObject.SetActive(false);
    _reset = _ui.Button(L("기본값", "Default"), Root, 200, 254, 72, 40, ResetColor);
    TextMeshProUGUI resetText = _reset.GetComponentInChildren<TextMeshProUGUI>();
    UiFactory.Stretch(resetText.rectTransform, 6, 0, 6, 0);
    resetText.fontSize = 14;
    resetText.textWrappingMode = TextWrappingModes.NoWrap;
    _error = _ui.Text("", Root, 16, 297, 256, 16, 11, UiPalette.Error);
    Gradient = new UiGradientBar(_ui, Root, this);
    Root.gameObject.SetActive(false);
  }

  public void SetDefault(Color color)
  {
    _hasDefault = true;
    _defaultColor = color;
  }

  public void Show(string title, Color value, bool opacity, Action<Color> changed)
  {
    _sourceMode = false;
    _followsHighlight = false;
    _sourceChanged = null;
    Gradient.Clear();
    _changed = changed;
    _allowOpacity = opacity;
    _title.text = title;
    if (!opacity)
      value.a = 1;
    Open(value);
  }

  /// <summary>
  /// Overlay element colors: follow the highlight color, keeping only their own
  /// opacity, or a custom color with opacity. Laid out as a column, the color
  /// bar on top makes gradients; <paramref name="keepStop"/> stays on the
  /// picked stop when the same color shows again, as after undo.
  /// </summary>
  public void ShowSource(
    string title,
    OverlayColorSettings value,
    Action<OverlayColorSettings> changed,
    bool keepStop = false
  )
  {
    value ??= OverlayColorSettings.HighlightColor();
    _sourceMode = true;
    _followsHighlight = value.Highlight;
    _changed = null;
    _sourceChanged = changed;
    _allowOpacity = true;
    _title.text = title;
    Gradient.Load(value.Clone(), keepStop);
    Color color = Gradient.WorkingColor();
    _source.Selected = value.Highlight ? 0 : 1;
    Open(color);
  }

  private void Open(Color value)
  {
    Layout();
    Color.RGBToHSV(value, out _h, out _s, out _v);
    Apply(value, true, false);
    Root.gameObject.SetActive(true);
    Root.SetAsLastSibling();
  }

  private void SetSource(bool highlight)
  {
    if (!_sourceMode || _followsHighlight == highlight)
      return;
    _followsHighlight = highlight;
    // Following the highlight or not changes the whole gradient at once.
    if (Gradient.IsGradient)
    {
      Gradient.SetHighlight(highlight);
      Layout();
      ShowColor(Gradient.WorkingColor());
      Gradient.Emit();
      return;
    }
    Color color = highlight ? UiTheme.Accent : _value;
    color.a = _value.a;
    Color.RGBToHSV(color, out _h, out _s, out _v);
    Layout();
    Apply(color, true, true);
  }

  // Classic pickers keep their original layout. Source pickers add the
  // Highlight/Custom row; following the highlight leaves only opacity.
  private void Layout()
  {
    bool custom = !_sourceMode || !_followsHighlight;
    float top = _sourceMode ? SourceRow : 0;
    _source.Root.gameObject.SetActive(_sourceMode);
    _planeRect.gameObject.SetActive(custom);
    _hue.gameObject.SetActive(custom);
    _hex.gameObject.SetActive(custom);
    _opacityLabel.gameObject.SetActive(!custom);
    _alpha.gameObject.SetActive(_allowOpacity);
    _opacity.gameObject.SetActive(_allowOpacity && !_externalOpacity);
    _opacitySlot.gameObject.SetActive(_allowOpacity && _externalOpacity);
    _reset.gameObject.SetActive(!_sourceMode && !_allowOpacity && _hasDefault);
    if (!_column)
      Gradient.Layout(0, 0);
    if (_column)
    {
      LayoutColumn(custom);
      return;
    }
    UiFactory.Place(_title.rectTransform, 16, 10, 208, 32);
    UiFactory.Place((RectTransform)_close.transform, 236, 6, 40, 40);
    UiFactory.Place(_source.Root, 16, 52, 256, 36);
    if (custom)
    {
      UiFactory.Place(_planeRect, 16, 52 + top, 256, 156);
      UiFactory.Place((RectTransform)_hue.transform, 6, 214 + top, 276, 36);
      UiFactory.Place((RectTransform)_alpha.transform, 6, 250 + top, 276, 36);
      float bottom = (_allowOpacity ? 294 : 254) + top;
      float hexWidth = _allowOpacity || _reset.gameObject.activeSelf ? 176 : 256;
      UiFactory.Place((RectTransform)_hex.transform, 16, bottom, hexWidth, 40);
      UiFactory.Place((RectTransform)_opacity.transform, 200, bottom, 72, 40);
      UiFactory.Place(_opacitySlot, 200, bottom, 72, 40);
      UiFactory.Place(_error.rectTransform, 16, bottom + 43, 256, 16);
    }
    else
    {
      UiFactory.Place((RectTransform)_alpha.transform, 6, 52 + top, 276, 36);
      UiFactory.Place(_opacityLabel.rectTransform, 16, 96 + top, 176, 40);
      UiFactory.Place((RectTransform)_opacity.transform, 200, 96 + top, 72, 40);
      UiFactory.Place(_opacitySlot, 200, 96 + top, 72, 40);
      UiFactory.Place(_error.rectTransform, 16, 139 + top, 256, 16);
    }
    Resize();
  }

  // Top to bottom in a column as wide as the color square, centered: the
  // square keeps its full size and HEX with opacity sits along the bottom.
  private void LayoutColumn(bool custom)
  {
    const float column = 256;
    if (GradientAvailable)
    {
      LayoutTwoColumns(custom);
      return;
    }
    float left = (_columnWidth - column) * .5f;
    Gradient.Layout(left, column);
    float top = 0;
    UiFactory.Place(_source.Root, left, top, column, 36);
    if (custom)
    {
      const float square = 156;
      float y = top + 44 + square + 6;
      UiFactory.Place(_planeRect, left, top + 44, column, square);
      UiFactory.Place((RectTransform)_hue.transform, left - 10, y, column + 20, 36);
      UiFactory.Place((RectTransform)_alpha.transform, left - 10, y + 32, column + 20, 36);
      UiFactory.Place(_error.rectTransform, left, y + 70, column, 14);
      float bottom = Height - 40;
      UiFactory.Place((RectTransform)_hex.transform, left, bottom, 176, 40);
      UiFactory.Place((RectTransform)_opacity.transform, left + 184, bottom, 72, 40);
      UiFactory.Place(_opacitySlot, left + 184, bottom, 72, 40);
    }
    else
    {
      // Following the highlight leaves only its opacity: a gradient's stops
      // differ only in opacity, which the label names for the picked stop.
      _opacityLabel.text = OpacityName();
      UiFactory.Place(_opacityLabel.rectTransform, left, top + 44, 176, 40);
      UiFactory.Place((RectTransform)_opacity.transform, left + 184, top + 44, 72, 40);
      UiFactory.Place(_opacitySlot, left + 184, top + 44, 72, 40);
      UiFactory.Place((RectTransform)_alpha.transform, left - 10, top + 92, column + 20, 36);
      UiFactory.Place(_error.rectTransform, left, top + 128, column, 14);
      UiFactory.Place(Gradient.Note, left, top + 142, column, UiGradientBar.NoteHeight);
    }
    Resize();
  }

  private void LayoutTwoColumns(bool custom)
  {
    const float column = ColumnWidth,
      x = RightColumn;
    Gradient.Layout(0, column);
    // Highlight/Custom sits under the bar in the left column.
    float source = Gradient.Height;
    UiFactory.Place(_source.Root, 0, source, column, 36);
    UiFactory.Place(Gradient.Note, 0, source + 44, column, UiGradientBar.NoteHeight);
    if (custom)
    {
      const float square = 156;
      UiFactory.Place(_planeRect, x, 0, column, square);
      UiFactory.Place((RectTransform)_hue.transform, x - 10, square + 6, column + 20, 36);
      UiFactory.Place((RectTransform)_alpha.transform, x - 10, square + 38, column + 20, 36);
      UiFactory.Place(_error.rectTransform, x, square + 76, column, 14);
      float bottom = ColumnHeight - 40;
      UiFactory.Place((RectTransform)_hex.transform, x, bottom, 176, 40);
      UiFactory.Place((RectTransform)_opacity.transform, x + 184, bottom, 72, 40);
      UiFactory.Place(_opacitySlot, x + 184, bottom, 72, 40);
    }
    else
    {
      _opacityLabel.text = OpacityName();
      UiFactory.Place(_opacityLabel.rectTransform, x, 0, 176, 40);
      UiFactory.Place((RectTransform)_opacity.transform, x + 184, 0, 72, 40);
      UiFactory.Place(_opacitySlot, x + 184, 0, 72, 40);
      UiFactory.Place((RectTransform)_alpha.transform, x - 10, 48, column + 20, 36);
      UiFactory.Place(_error.rectTransform, x, 84, column, 14);
    }
    Resize();
  }

  // Following the highlight leaves only the opacity; a gradient's label names
  // the picked stop.
  private string OpacityName()
  {
    if (!Gradient.IsGradient)
      return L("불투명도", "Opacity");
    int rank = Gradient.PickedRank;
    return L(rank + "번째 색 불투명도", "Color " + rank + " opacity");
  }

  private void Resize()
  {
    Root.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, CurrentWidth);
    Root.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Height);
  }

  /// <summary>
  /// Takes the color again after the editor changed it (handles, the dock,
  /// undo), keeping the picked stop. The controls follow without reporting.
  /// </summary>
  public void Refresh(OverlayColorSettings value)
  {
    if (!Visible || !_sourceMode || value == null)
      return;
    bool wasGradient = Gradient.IsGradient;
    Gradient.Reload(value.Clone());
    bool layout = wasGradient != Gradient.IsGradient;
    if (_followsHighlight != value.Highlight)
    {
      _followsHighlight = value.Highlight;
      _source.Selected = value.Highlight ? 0 : 1;
      layout = true;
    }
    if (layout)
      Layout();
    // Undo can change the color underneath; the controls show it again. A
    // color that already shows keeps its hue, which grays would lose.
    Color shown = Gradient.WorkingColor();
    bool differs =
      Mathf.Abs(shown.r - _value.r) > .002f
      || Mathf.Abs(shown.g - _value.g) > .002f
      || Mathf.Abs(shown.b - _value.b) > .002f
      || (Gradient.IsGradient && Mathf.Abs(shown.a - _value.a) > .002f);
    if (differs)
      ShowColor(shown);
    else
      Gradient.Refresh();
  }

  /// <summary>Moves the opacity to <paramref name="alpha"/> without reporting a change.</summary>
  public void ShowOpacity(float alpha)
  {
    alpha = Mathf.Clamp01(alpha);
    if (!_allowOpacity || Mathf.Approximately(_value.a, alpha))
      return;
    Color color = _value;
    color.a = alpha;
    Apply(color, true, false);
  }

  public void Hide()
  {
    if (!Visible)
      return;
    Root.gameObject.SetActive(false);
    InputGate.SetTextEditing(false);
    Hidden?.Invoke();
  }

  public bool Commit()
  {
    if (!Visible)
      return true;
    if (_sourceMode && _followsHighlight)
    {
      float alpha = _value.a;
      if (!_externalOpacity && !TryReadOpacity(_opacity.text, out alpha))
      {
        _error.text = L("불투명도를 0–100%로 입력하세요", "Enter opacity from 0 to 100%");
        Focus(_opacity);
        return false;
      }
      Color accent = UiTheme.Accent;
      accent.a = alpha;
      Apply(accent, true, true);
      return true;
    }
    if (!TryReadHex(_hex.text, out Color color))
    {
      _error.text = L("BA712B처럼 6자리로 입력하세요", "Use six hex digits, e.g. BA712B");
      Focus(_hex);
      return false;
    }
    color.a = _value.a;
    if (_allowOpacity && !_externalOpacity)
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
    float alphaBefore = _value.a;
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
    {
      _changed?.Invoke(color);
      if (_sourceChanged != null)
        _sourceChanged(Gradient.TakeColor(color, alphaBefore));
    }
    Gradient.Refresh();
    _ui.RefreshTheme();
    _syncing = false;
  }

  /// <summary>Shows <paramref name="color"/> in the controls without reporting a change.</summary>
  private void ShowColor(Color color)
  {
    Color.RGBToHSV(color, out _h, out _s, out _v);
    Apply(color, true, false);
  }

  bool IGradientBarHost.GradientShown => GradientAvailable;

  bool IGradientBarHost.FollowsHighlight => _followsHighlight;

  Color IGradientBarHost.Value => _value;

  void IGradientBarHost.ShowColor(Color color) => ShowColor(color);

  void IGradientBarHost.Relayout() => Layout();

  void IGradientBarHost.Report(OverlayColorSettings value) => _sourceChanged?.Invoke(value);

  public void Dispose()
  {
    _ui.Dispose();
    UiFactory.Destroy(Root.gameObject);
  }
}
