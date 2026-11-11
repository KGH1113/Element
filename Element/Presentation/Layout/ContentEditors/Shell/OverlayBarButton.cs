using System;
using Element.Presentation.Settings;
using Element.Presentation.Ui.Popovers;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Element.Presentation.Layout.ContentEditors.Shell;

/// <summary>
/// One control in an editor's top bar: a button that says what it is and
/// what it holds, such as <c>● Fill</c> or <c>═ Border width 2 px</c>. It
/// looks clickable at rest, lights up as a whole on hover, and stays lit while
/// what it opened is open. A switch button shows its state in a small switch
/// after the name, as the settings' switches do.
/// </summary>
internal sealed class OverlayBarButton
{
  public const float Height = 32;
  private const float Padding = 12,
    LeadSize = 18,
    LeadGap = 7,
    ValueGap = 6,
    TrackWidth = 30,
    TrackHeight = 18,
    ThumbInset = 4,
    ThumbSize = TrackHeight - ThumbInset * 2;

  private readonly Button _button;
  private readonly bool _reduceMotion;
  private readonly RectTransform _lead;
  private readonly float _leadWidth = LeadSize,
    _leadHeight = LeadSize;
  private readonly TextMeshProUGUI _label,
    _value,
    _unit;
  private readonly SettingsCapsuleGraphic _swatch;
  private readonly SettingsGradientPreview _swatchGradient;
  private readonly LayoutActionIcon _icon;
  private readonly SettingsCapsuleGraphic _track,
    _thumb;
  private readonly SettingsSwitchMotion _thumbMotion;
  private bool _active;
  private int _drop;

  /// <param name="lead">A swatch (<paramref name="swatch"/>), an icon or a glyph before the name; none when all are unset.</param>
  /// <param name="toggle">A switch after the name, set with <see cref="SetSwitch"/>.</param>
  public OverlayBarButton(
    SettingsUiFactory ui,
    Transform parent,
    string name,
    string label,
    Action click,
    bool reduceMotion,
    string unit = "",
    bool swatch = false,
    LayoutAction? icon = null,
    string glyph = null,
    bool toggle = false
  )
  {
    _reduceMotion = reduceMotion;
    _button = EditorCapsules.CapsuleButton(ui, parent, name, "", 0, 0, 10, Height, 14, click, false, reduceMotion);
    Root = (RectTransform)_button.transform;
    if (swatch)
    {
      _lead = ui.Rect("Swatch", Root, 0, 0, LeadSize, LeadSize);
      ui.Capsule("Ring", _lead, 0, 0, LeadSize, LeadSize, new Color(1, 1, 1, .5f));
      _swatch = ui.Capsule("Color", _lead, 2, 2, LeadSize - 4, LeadSize - 4, Color.white);
      // A gradient shows inside the same circle.
      _swatch.gameObject.AddComponent<Mask>().showMaskGraphic = true;
      _swatchGradient = ui.Rect("Gradient", _swatch.transform, 0, 0, LeadSize - 4, LeadSize - 4)
        .gameObject.AddComponent<SettingsGradientPreview>();
      _swatchGradient.raycastTarget = false;
      _swatchGradient.Columns = _swatchGradient.Rows = 6;
      _swatchGradient.Cell = 4;
      _swatchGradient.gameObject.SetActive(false);
    }
    else if (icon.HasValue)
    {
      LayoutActionIcon graphic = ui.Rect("Icon", Root, 0, 0, LeadSize, LeadSize)
        .gameObject.AddComponent<LayoutActionIcon>();
      graphic.Kind = icon.Value;
      graphic.color = SettingsUiFactory.Foreground;
      graphic.raycastTarget = false;
      _lead = graphic.rectTransform;
      _icon = graphic;
    }
    else if (!string.IsNullOrEmpty(glyph))
    {
      TextMeshProUGUI text = ui.Text(glyph, Root, 0, 0, LeadSize, Height, 15, SettingsUiFactory.Muted);
      text.alignment = TextAlignmentOptions.Midline;
      text.textWrappingMode = TextWrappingModes.NoWrap;
      _lead = text.rectTransform;
      // A glyph takes the room it needs, over the full height of the button.
      _leadWidth = Mathf.Max(LeadSize, Mathf.Ceil(text.GetPreferredValues(glyph).x) + 1);
      _leadHeight = Height;
    }
    _label = Text(ui, label, 14, SettingsUiFactory.Foreground);
    _value = Text(ui, "", 14, SettingsUiFactory.Foreground);
    _unit = Text(ui, unit, 12, SettingsUiFactory.Muted);
    if (toggle)
    {
      _track = ui.Capsule("Switch", Root, 0, 0, TrackWidth, TrackHeight, SwitchOff);
      _track.raycastTarget = false;
      _thumb = ui.Capsule("Thumb", _track.transform, ThumbInset, ThumbInset, ThumbSize, ThumbSize, Color.white);
      _thumb.raycastTarget = false;
      _thumbMotion = _thumb.gameObject.AddComponent<SettingsSwitchMotion>();
    }
    Set(null, false, false);
  }

  private static Color SwitchOff => new(.3f, .3f, .3f);

  public RectTransform Root { get; }

  public Button Button => _button;

  public float Width { get; private set; }

  public string Value => _value.text;

  /// <summary>The shown value came from a formula.</summary>
  public bool Computed { get; private set; }

  /// <summary>Lit while what the button opened is open.</summary>
  public bool Active => _active;

  public Color Swatch
  {
    set
    {
      if (_swatch != null)
        _swatch.color = value;
    }
  }

  /// <summary>A gradient in the swatch instead of its color; null shows the color.</summary>
  public Overlay.Elements.OverlayResolvedGradient SwatchGradient
  {
    set
    {
      if (_swatchGradient == null)
        return;
      _swatchGradient.gameObject.SetActive(value != null);
      if (value != null)
        _swatchGradient.SetGradient(value);
    }
  }

  /// <summary>
  /// Shows <paramref name="value"/> after the name (none when null);
  /// <paramref name="computed"/> marks a result of a formula, <paramref name="active"/>
  /// while what the button opened is open.
  /// </summary>
  public void Set(string value, bool computed, bool active)
  {
    Computed = computed;
    float x = Padding;
    if (_lead != null)
    {
      SettingsUiFactory.Place(_lead, x, (Height - _leadHeight) * .5f, _leadWidth, _leadHeight);
      x += _leadWidth + LeadGap;
    }
    x = Place(_label, x);
    bool hasValue = !string.IsNullOrEmpty(value);
    _value.gameObject.SetActive(hasValue);
    _unit.gameObject.SetActive(hasValue && _unit.text.Length > 0);
    if (hasValue)
    {
      _value.text = value;
      _value.color = computed ? SettingsPalette.SoftAccent : SettingsUiFactory.Foreground;
      x = Place(_value, x + ValueGap);
      if (_unit.text.Length > 0)
        x = Place(_unit, x + 2);
    }
    if (_track != null)
    {
      x += ValueGap + 2;
      SettingsUiFactory.Place(_track.rectTransform, x, (Height - TrackHeight) * .5f, TrackWidth, TrackHeight);
      x += TrackWidth - 2;
    }
    Width = x + Padding;
    SettingsUiFactory.Place(Root, Root.anchoredPosition.x, -Root.anchoredPosition.y, Width, Height);
    if (_active != active)
    {
      _active = active;
      Paint();
    }
  }

  /// <summary>
  /// The switch on, off, or (null) halfway for a selection that mixes both.
  /// Unless <paramref name="available"/>, the button reads muted: there is
  /// nothing for it to act on, and a click only says why.
  /// </summary>
  public void SetSwitch(bool? on, bool available)
  {
    if (_track == null)
      return;
    Color accent = SettingsPalette.SoftAccent;
    float alpha = available ? 1 : .4f;
    Color track = on == false ? SwitchOff : SettingsPalette.AccentSurface,
      thumb = on == false ? SettingsUiFactory.Foreground : accent;
    track.a *= alpha;
    thumb.a *= alpha;
    _track.color = track;
    _thumb.color = thumb;
    float travel = TrackWidth - ThumbSize - ThumbInset * 2;
    _thumbMotion.Set(
      ThumbInset
        + (
          on == null ? travel * .5f
          : on.Value ? travel
          : 0
        ),
      _reduceMotion
    );
    Color label = available ? SettingsUiFactory.Foreground : SettingsUiFactory.Muted;
    _label.color = label;
    if (_icon != null)
      _icon.color = label;
  }

  public void Place(float x, float y) => SettingsUiFactory.Place(Root, x, y, Width, Height);

  public bool Contains(Vector2 screen) => PopoverPlacement.Contains(Root, screen);

  /// <summary>0 nothing, 1 a dragged value can land here, -1 it can't.</summary>
  public void SetDrop(int state)
  {
    if (_drop == state)
      return;
    _drop = state;
    Paint();
  }

  private void Paint()
  {
    Color accent = SettingsPalette.SoftAccent;
    Color rest =
      _drop > 0 ? new Color(accent.r, accent.g, accent.b, .38f)
      : _drop < 0 ? new Color(LayoutColors.Warning.r, LayoutColors.Warning.g, LayoutColors.Warning.b, .3f)
      : _active ? new Color(accent.r, accent.g, accent.b, .3f)
      : new Color(1, 1, 1, .07f);
    ColorBlock colors = _button.colors;
    colors.normalColor = colors.selectedColor = rest;
    colors.highlightedColor = _active ? new Color(accent.r, accent.g, accent.b, .42f) : new Color(1, 1, 1, .15f);
    colors.fadeDuration = _reduceMotion ? 0 : .08f;
    ((SettingsButton)_button).SetPalette(colors);
  }

  private TextMeshProUGUI Text(SettingsUiFactory ui, string text, float size, Color color)
  {
    TextMeshProUGUI label = ui.Text(text, Root, 0, 0, 10, Height, size, color);
    label.textWrappingMode = TextWrappingModes.NoWrap;
    label.alignment = TextAlignmentOptions.MidlineLeft;
    return label;
  }

  private static float Place(TextMeshProUGUI text, float x)
  {
    float width = Mathf.Ceil(text.GetPreferredValues(text.text).x) + 1;
    SettingsUiFactory.Place(text.rectTransform, x, 0, width, Height);
    return x + width;
  }
}
