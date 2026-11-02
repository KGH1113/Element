using System;
using System.Globalization;
using Element.Presentation.Settings;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Element.Presentation.Layout;

/// <summary>
/// A number beside the preview as it comes out: <c>W 160 px</c>. A number
/// worked out from a formula reads in the accent; the formula itself is typed
/// in the input dock, which a click points at it. The pill is the button, so
/// it lights up exactly where it is drawn. It grows with what it shows, so
/// callers lay it out after <see cref="Set"/>.
/// </summary>
internal sealed class OverlayFormulaField
{
  public const float Height = 32;
  private const float MinimumBody = 34,
    MaximumText = 240;

  private static readonly Color Rest = new(.2f, .2f, .2f, .96f);
  private static readonly Color Hover = new(.27f, .27f, .27f, .98f);

  private readonly Button _body;
  private readonly bool _reduceMotion;
  private readonly TextMeshProUGUI _text,
    _unit,
    _prefix;
  private float _unitWidth,
    _prefixWidth;
  private float _textWidth;
  private bool _active;
  private int _drop = int.MinValue;

  public OverlayFormulaField(
    SettingsUiFactory ui,
    Transform parent,
    string name,
    string unit,
    Action activate,
    bool reduceMotion,
    string prefix = ""
  )
  {
    _reduceMotion = reduceMotion;
    _body = OverlayEditorUi.CapsuleButton(ui, parent, name, "", 0, 0, 10, Height, 14, activate, false, reduceMotion);
    Root = (RectTransform)_body.transform;
    _prefix = ui.Text(prefix, Root, 0, 0, 10, Height, 12, SettingsUiFactory.Muted);
    _prefix.textWrappingMode = TextWrappingModes.NoWrap;
    _prefixWidth = Measure(_prefix, 5);
    _text = ui.Text("", Root, 0, 0, 10, Height, 15);
    _text.alignment = TextAlignmentOptions.MidlineRight;
    _text.textWrappingMode = TextWrappingModes.NoWrap;
    _text.overflowMode = TextOverflowModes.Ellipsis;
    _unit = ui.Text(unit, Root, 0, 0, 10, Height, 12, SettingsUiFactory.Muted);
    _unit.textWrappingMode = TextWrappingModes.NoWrap;
    _unitWidth = Measure(_unit, 3);
    SetDrop(0);
  }

  public RectTransform Root { get; }

  public float Width { get; private set; }

  public string Name => Root.name;

  public string Text => _text.text;

  /// <summary>The shown number came from a formula.</summary>
  public bool Computed { get; private set; }

  /// <summary>
  /// Shows a result; <paramref name="computed"/> when a formula produced it,
  /// <paramref name="active"/> while the input dock edits it.
  /// </summary>
  public void Set(string text, bool computed, bool active)
  {
    text ??= "";
    Computed = computed;
    _text.text = text;
    _text.color = computed ? SettingsPalette.SoftAccent : SettingsUiFactory.Foreground;
    _textWidth = Mathf.Min(MaximumText, Mathf.Ceil(_text.GetPreferredValues(text).x) + 1);
    // A field made while hidden (as in a closed color picker) is measured
    // once it shows.
    if (_prefixWidth == 0)
      _prefixWidth = Measure(_prefix, 5);
    if (_unitWidth == 0)
      _unitWidth = Measure(_unit, 3);
    Width = Mathf.Max(MinimumBody, _prefixWidth + _textWidth + _unitWidth + 22);
    float left = (Width - _prefixWidth - _textWidth - _unitWidth) * .5f;
    SettingsUiFactory.Place(_prefix.rectTransform, left, 0, _prefixWidth, Height);
    SettingsUiFactory.Place(_text.rectTransform, left + _prefixWidth, 0, _textWidth, Height);
    SettingsUiFactory.Place(_unit.rectTransform, left + _prefixWidth + _textWidth + 3, 0, _unitWidth, Height);
    if (_active != active)
    {
      _active = active;
      Paint();
    }
  }

  // Zero for no text, and while hidden, when the text can't be measured yet.
  private static float Measure(TextMeshProUGUI label, float gap) =>
    label.text.Length > 0 && label.gameObject.activeInHierarchy
      ? Mathf.Ceil(label.GetPreferredValues(label.text).x) + gap
      : 0;

  public void Place(float x, float y) => SettingsUiFactory.Place(Root, x, y, Width, Height);

  /// <summary>A result as a field shows it: up to one decimal.</summary>
  public static string Number(double value) =>
    double.IsNaN(value) || double.IsInfinity(value) ? "—" : value.ToString("0.#", CultureInfo.InvariantCulture);

  /// <summary>0 nothing, 1 a dragged value can land here, -1 it can't.</summary>
  public void SetDrop(int state)
  {
    if (_drop == state)
      return;
    _drop = state;
    Paint();
  }

  public bool Contains(Vector2 screen) => OverlayEditorUi.Contains(Root, screen);

  public void SetVisible(bool visible)
  {
    if (Root.gameObject.activeSelf != visible)
      Root.gameObject.SetActive(visible);
  }

  // The pill itself carries every state, so hover covers exactly what is drawn.
  private void Paint()
  {
    Color accent = SettingsPalette.SoftAccent;
    Color rest =
      _drop > 0 ? Color.Lerp(Rest, accent, .5f)
      : _drop < 0 ? Color.Lerp(Rest, OverlayEditorUi.Warning, .4f)
      : _active ? Color.Lerp(Rest, accent, .38f)
      : Rest;
    ColorBlock colors = _body.colors;
    colors.normalColor = colors.selectedColor = rest;
    colors.highlightedColor = _active ? Color.Lerp(Rest, accent, .5f) : Hover;
    colors.pressedColor = Color.Lerp(Hover, Color.white, .1f);
    colors.fadeDuration = _reduceMotion ? 0 : .08f;
    ((SettingsButton)_body).SetPalette(colors);
  }
}
