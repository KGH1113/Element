using System;
using System.Globalization;
using Element.Configuration.Overlay;
using Element.Presentation.Ui.Theme;
using ElementUI;
using UnityEngine;
using static Element.Presentation.Ui.Theme.UiLocale;

namespace Element.Presentation.Ui.ColorPicker;

/// <summary>
/// What a color picker shows and does. One picker serves the shared overlay
/// highlight, independent RGBA colors (planets, mods) and overlay element
/// colors, which follow the highlight color or are custom, with a color bar
/// for gradients. It keeps the color as hue, saturation, brightness and
/// opacity; the HEX and opacity fields type it. Consumers decide what the
/// color affects. <see cref="ColorPickerCardView"/> and
/// <see cref="ColorPickerColumnsView"/> draw it.
/// </summary>
internal sealed class ColorPickerViewModel : ViewModel, IGradientBarHost
{
  private Action<Color> _changed;
  private Action<OverlayColorSettings> _sourceChanged;
  private Color _value,
    _defaultColor;
  private float _h,
    _s,
    _v;
  private int _rank;
  private bool _active,
    _allowOpacity,
    _hasDefault,
    _sourceMode,
    _followsHighlight,
    _externalOpacity,
    _gradientShown;

  public ColorPickerViewModel()
  {
    Reset = Command(ResetColor);
    AddGradient = Command(() => Gradient?.MakeGradient());
    Close = Command(() => CloseRequested?.Invoke());
  }

  /// <summary>The color bar over the controls, for overlay colors; null where there is none.</summary>
  public IGradientBar Gradient { get; set; }

  /// <summary>The picker's close button was pressed; its owner hides it.</summary>
  public Action CloseRequested;

  /// <summary>What shows changed, so the picker may need another size.</summary>
  public Action LayoutChanged;

  // ---- What it shows ----

  /// <summary>The picker's title, such as "Body color".</summary>
  public readonly State<string> Title = new("");

  /// <summary>The Highlight/Custom choice shows: an overlay element's color.</summary>
  public readonly State<bool> SourceShown = new(false);

  /// <summary>Its own color, with the square, hue and HEX; false while following the highlight, which leaves only opacity.</summary>
  public readonly State<bool> Custom = new(true);

  /// <summary>The opacity slider and box show: the color has an alpha.</summary>
  public readonly State<bool> OpacityShown = new(true);

  /// <summary>The owner shows the opacity in its own box (<see cref="ExternalOpacity"/>) instead of the typed one.</summary>
  public readonly State<bool> OpacityOutside = new(false);

  /// <summary>Default puts the default color back: an opaque color that has one.</summary>
  public readonly State<bool> ResetShown = new(false);

  /// <summary>The color is a gradient, shown on the color bar.</summary>
  public readonly State<bool> GradientOn = new(false);

  /// <summary>The picked color of a gradient has a position its owner shows as a number.</summary>
  public readonly State<bool> PositionShown = new(false);

  /// <summary>The color bar's height, which grows with a gradient's kinds.</summary>
  public readonly State<float> BarHeight = new(0);

  /// <summary>The opacity's name while following the highlight: a gradient's names the picked color.</summary>
  public readonly State<string> OpacityLabel = new(L("불투명도", "Opacity"));

  /// <summary>Hue, saturation and brightness, each 0–1.</summary>
  public readonly State<Vector3> Hsv = new(Vector3.zero);

  /// <summary>The opacity, 0–1.</summary>
  public readonly State<float> Alpha = new(1);

  /// <summary>The color, opaque: the swatch in the HEX field and the marker on the square.</summary>
  public readonly State<Color> Swatch = new(Color.white);

  /// <summary>The HEX field's words.</summary>
  public readonly State<string> HexText = new("");

  /// <summary>The opacity field's words, in percent.</summary>
  public readonly State<string> OpacityText = new("100");

  /// <summary>Why the typed color can't be used; empty when it can.</summary>
  public readonly State<string> Error = new("");

  /// <summary>Goes up each time the HEX or the opacity field should take the keyboard to be corrected.</summary>
  public readonly State<int> FocusHex = new(0),
    FocusOpacity = new(0);

  /// <summary>The Highlight/Custom choice: 0 follows the highlight, 1 is custom.</summary>
  public readonly State<int> SourceIndex = new(1);

  /// <summary>The HEX field: a six-digit color, applied as it is typed.</summary>
  public string Hex
  {
    [Pure]
    get => HexText.Value;
    set => TypeHex(value);
  }

  /// <summary>The opacity field: 0–100, applied as it is typed.</summary>
  public string Opacity
  {
    [Pure]
    get => OpacityText.Value;
    set => TypeOpacity(value);
  }

  /// <summary>Highlight/Custom: 0 follows the highlight color, 1 is a custom color.</summary>
  public int Source
  {
    [Pure]
    get => SourceIndex.Value;
    set => SetSource(value == 0);
  }

  /// <summary>Puts the default color back.</summary>
  public Command Reset { get; }

  /// <summary>Makes a solid color a gradient, with a second color at its end.</summary>
  public Command AddGradient { get; }

  /// <summary>Asks the owner to close the picker.</summary>
  public Command Close { get; }

  /// <summary>The color the controls show.</summary>
  public Color Value => _value;

  /// <summary>Opened with <see cref="ShowSource"/>: an overlay element's color.</summary>
  public bool SourceMode => _sourceMode;

  /// <summary>
  /// Highlight or custom is picked in the input dock's values, not here: the
  /// Highlight/Custom choice doesn't show, and following the highlight says
  /// where to pick a custom color.
  /// </summary>
  public bool SourceInDock
  {
    get => _sourceInDock;
    set
    {
      if (_sourceInDock == value)
        return;
      _sourceInDock = value;
      UpdateShape();
    }
  }

  private bool _sourceInDock;

  /// <summary>Following the highlight color.</summary>
  public bool FollowsHighlight => _followsHighlight;

  /// <summary>The owner shows and edits the opacity itself.</summary>
  public bool ExternalOpacity
  {
    get => _externalOpacity;
    set
    {
      if (_externalOpacity == value)
        return;
      _externalOpacity = value;
      UpdateShape();
    }
  }

  /// <summary>The color bar shows, in a picker laid out as two columns.</summary>
  public bool GradientShown
  {
    get => _gradientShown;
    set
    {
      if (_gradientShown == value)
        return;
      _gradientShown = value;
      UpdateShape();
    }
  }

  public void SetDefault(Color color)
  {
    _hasDefault = true;
    _defaultColor = color;
  }

  /// <summary>An independent color; <paramref name="opacity"/> edits its alpha too, otherwise it stays opaque.</summary>
  public void Show(string title, Color value, bool opacity, Action<Color> changed)
  {
    _sourceMode = false;
    _followsHighlight = false;
    _sourceChanged = null;
    Gradient?.Clear();
    _changed = changed;
    _allowOpacity = opacity;
    Title.Value = title;
    if (!opacity)
      value.a = 1;
    Open(value);
  }

  /// <summary>
  /// An overlay element's color: following the highlight color, keeping only
  /// its own opacity, or a custom color with opacity, solid or a gradient.
  /// <paramref name="keepStop"/> stays on the picked color when the same
  /// color shows again, as after undo.
  /// </summary>
  public void ShowSource(string title, OverlayColorSettings value, Action<OverlayColorSettings> changed, bool keepStop)
  {
    value ??= OverlayColorSettings.HighlightColor();
    _sourceMode = true;
    _followsHighlight = value.Highlight;
    _changed = null;
    _sourceChanged = changed;
    _allowOpacity = true;
    Title.Value = title;
    Color color = value.Highlight ? UiTheme.Accent : Color.white;
    if (Gradient != null)
    {
      Gradient.Load(value.Clone(), keepStop);
      color = Gradient.WorkingColor();
    }
    Open(color);
  }

  private void Open(Color value)
  {
    _active = true;
    // A field asked to take the keyboard for an earlier color isn't asked again.
    FocusHex.Value = 0;
    FocusOpacity.Value = 0;
    UpdateShape();
    Color.RGBToHSV(value, out _h, out _s, out _v);
    Apply(value, true, false);
  }

  /// <summary>The picker closed: typing in it no longer reports.</summary>
  public void Closed() => _active = false;

  /// <summary>Highlight/Custom picked.</summary>
  public void SetSource(bool highlight)
  {
    if (!_sourceMode || _followsHighlight == highlight)
      return;
    _followsHighlight = highlight;
    // Following the highlight or not changes the whole gradient at once.
    if (Gradient != null && Gradient.IsGradient)
    {
      Gradient.SetHighlight(highlight);
      UpdateShape();
      ShowColor(Gradient.WorkingColor());
      Gradient.Emit();
      return;
    }
    Color color = highlight ? UiTheme.Accent : _value;
    color.a = _value.a;
    Color.RGBToHSV(color, out _h, out _s, out _v);
    UpdateShape();
    Apply(color, true, true);
  }

  /// <summary>
  /// Takes the color again after the editor changed it (handles, the dock,
  /// undo), keeping the picked color. The controls follow without reporting.
  /// </summary>
  public void Refresh(OverlayColorSettings value)
  {
    if (!_active || !_sourceMode || value == null || Gradient == null)
      return;
    bool wasGradient = Gradient.IsGradient;
    Gradient.Reload(value.Clone());
    bool shape = wasGradient != Gradient.IsGradient;
    if (_followsHighlight != value.Highlight)
    {
      _followsHighlight = value.Highlight;
      shape = true;
    }
    if (shape)
      UpdateShape();
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

  /// <summary>
  /// Applies what is typed. False, with the reason shown and the field to
  /// fix taking the keyboard, while the HEX or the opacity can't be read.
  /// </summary>
  public bool Commit()
  {
    if (!_active)
      return true;
    if (_sourceMode && _followsHighlight)
    {
      float alpha = _value.a;
      if (!_externalOpacity && !TryReadOpacity(OpacityText.Value, out alpha))
      {
        Refuse(L("불투명도를 0–100%로 입력하세요", "Enter opacity from 0 to 100%"), FocusOpacity);
        return false;
      }
      Color accent = UiTheme.Accent;
      accent.a = alpha;
      Apply(accent, true, true);
      return true;
    }
    if (!TryReadHex(HexText.Value, out Color color))
    {
      Refuse(L("BA712B처럼 6자리로 입력하세요", "Use six hex digits, e.g. BA712B"), FocusHex);
      return false;
    }
    color.a = _value.a;
    if (_allowOpacity && !_externalOpacity)
    {
      if (!TryReadOpacity(OpacityText.Value, out float opacity))
      {
        Refuse(L("불투명도를 0–100%로 입력하세요", "Enter opacity from 0 to 100%"), FocusOpacity);
        return false;
      }
      color.a = opacity;
    }
    Apply(color, true, true);
    return true;
  }

  private void Refuse(string reason, State<int> field)
  {
    Error.Value = reason;
    field.Value++;
  }

  // ---- What the controls do ----

  /// <summary>A point on the square: saturation across, brightness up.</summary>
  public void PickSaturationValue(float saturation, float value)
  {
    _s = saturation;
    _v = value;
    ApplyHsv();
  }

  /// <summary>A hue on the hue bar.</summary>
  public void PickHue(float hue)
  {
    _h = hue;
    ApplyHsv();
  }

  /// <summary>An opacity on the opacity bar.</summary>
  public void PickAlpha(float alpha)
  {
    Color color = _value;
    color.a = alpha;
    Apply(color, true, true);
  }

  private void TypeHex(string text)
  {
    HexText.Value = text ?? "";
    Error.Value = "";
    if (!TryReadHex(text, out Color color))
      return;
    color.a = _value.a;
    Color.RGBToHSV(color, out _h, out _s, out _v);
    Apply(color, false, true);
  }

  private void TypeOpacity(string text)
  {
    OpacityText.Value = text ?? "";
    Error.Value = "";
    if (!TryReadOpacity(text, out float alpha))
      return;
    Color color = _value;
    color.a = alpha;
    Apply(color, false, true);
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
    Hsv.Value = new Vector3(_h, _s, _v);
    Alpha.Value = color.a;
    if (updateText)
    {
      HexText.Value = ColorUtility.ToHtmlStringRGB(color);
      OpacityText.Value = Mathf.RoundToInt(color.a * 100).ToString(CultureInfo.InvariantCulture);
    }
    Color opaque = color;
    opaque.a = 1;
    Swatch.Value = opaque;
    Error.Value = "";
    if (notify && _active)
    {
      _changed?.Invoke(color);
      if (_sourceChanged != null && Gradient != null)
        _sourceChanged(Gradient.TakeColor(color, alphaBefore));
    }
    Gradient?.Refresh();
  }

  /// <summary>Shows <paramref name="color"/> in the controls without reporting a change.</summary>
  public void ShowColor(Color color)
  {
    Color.RGBToHSV(color, out _h, out _s, out _v);
    Apply(color, true, false);
    // Another color may be picked: the opacity names it.
    if (_rank != (Gradient != null && Gradient.IsGradient ? Gradient.PickedRank : 0))
      OpacityLabel.Value = OpacityName();
  }

  // What shows follows the mode: source pickers add Highlight/Custom, and
  // following the highlight leaves only opacity.
  private void UpdateShape()
  {
    bool gradient = _gradientShown && Gradient != null && Gradient.IsGradient;
    SourceShown.Value = _sourceMode && !_sourceInDock;
    SourceIndex.Value = _followsHighlight ? 0 : 1;
    Custom.Value = !_sourceMode || !_followsHighlight;
    OpacityShown.Value = _allowOpacity;
    OpacityOutside.Value = _allowOpacity && _externalOpacity;
    ResetShown.Value = !_sourceMode && !_allowOpacity && _hasDefault;
    GradientOn.Value = gradient;
    PositionShown.Value = gradient && Gradient.HasPosition;
    BarHeight.Value = Gradient?.Height ?? 0;
    OpacityLabel.Value = OpacityName();
    // The bar shows its kinds and pill only with a gradient, and only in an overlay color's picker.
    if (_gradientShown)
      Gradient?.Relayout();
    LayoutChanged?.Invoke();
  }

  // Following the highlight leaves only the opacity; a gradient's label names
  // the picked color.
  private string OpacityName()
  {
    if (!_gradientShown || Gradient == null || !Gradient.IsGradient)
    {
      _rank = 0;
      return L("불투명도", "Opacity");
    }
    _rank = Gradient.PickedRank;
    return L(_rank + "번째 색 불투명도", "Color " + _rank + " opacity");
  }

  public static bool TryReadHex(string text, out Color color)
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

  public static bool TryReadOpacity(string text, out float opacity)
  {
    bool valid =
      int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int percent)
      && percent >= 0
      && percent <= 100;
    opacity = percent / 100f;
    return valid;
  }

  // ---- The color bar's host ----

  bool IGradientBarHost.GradientShown => _gradientShown && _sourceMode;

  Color IGradientBarHost.Value => _value;

  void IGradientBarHost.ShowColor(Color color) => ShowColor(color);

  void IGradientBarHost.Relayout() => UpdateShape();

  void IGradientBarHost.Report(OverlayColorSettings value) => _sourceChanged?.Invoke(value);
}
