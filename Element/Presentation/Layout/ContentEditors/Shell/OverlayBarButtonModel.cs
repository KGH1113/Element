using System;
using System.Collections.Generic;
using Element.Presentation.Overlay.Elements;
using Element.Presentation.Ui.Graphics;
using Element.Presentation.Ui.Theme;
using ElementUI;
using UnityEngine;

namespace Element.Presentation.Layout.ContentEditors.Shell;

/// <summary>
/// One control in an editor's top bar: a button that says what it is and
/// what it holds, such as <c>● Fill</c> or <c>═ Border width 2 px</c>, and
/// what a click does. It stays lit while what it opened is open, and lights
/// up as a place a dragged value can (or can't) land. A switch button shows
/// its state in a small switch after the name. The part it belongs to sets
/// what it shows; <see cref="OverlayBarButtonView"/> draws it.
/// </summary>
internal sealed class OverlayBarButtonModel : ViewModel
{
  private static readonly UiColor Rest = new Color(1, 1, 1, .07f);
  private readonly UiColor _lit,
    _accepts,
    _refuses,
    _computed;
  private string _value = "";
  private bool _active;
  private int _drop;

  /// <param name="name">Names the drawn button: the bar finds it by this, and so do the checks.</param>
  /// <param name="label">What it is, such as "Fill".</param>
  /// <param name="click">What a click does: open its picker or number, or switch.</param>
  /// <param name="value">Whether it shows what it holds after its name, set with <see cref="Set"/>.</param>
  /// <param name="unit">After the value, such as "px".</param>
  /// <param name="swatch">A color swatch before the name, set with <see cref="SetSwatch"/>.</param>
  /// <param name="icon">An icon before the name.</param>
  /// <param name="glyph">Letters before the name, such as "Aa".</param>
  /// <param name="toggle">A switch after the name, set with <see cref="SetSwitch"/>.</param>
  public OverlayBarButtonModel(
    string name,
    string label,
    Action click,
    bool value = false,
    string unit = null,
    bool swatch = false,
    UiIconKind? icon = null,
    string glyph = null,
    bool toggle = false
  )
  {
    Name = name;
    Label = label;
    ShowsValue = value;
    Unit = value && !string.IsNullOrEmpty(unit) ? unit : null;
    Swatch = swatch;
    Icon = swatch ? null : icon;
    Glyph = swatch || icon.HasValue || string.IsNullOrEmpty(glyph) ? null : glyph;
    Toggle = toggle;
    Color accent = UiPalette.SoftAccent,
      error = UiPalette.Error;
    _lit = new Color(accent.r, accent.g, accent.b, .3f);
    _accepts = new Color(accent.r, accent.g, accent.b, .38f);
    _refuses = new Color(error.r, error.g, error.b, .3f);
    _computed = accent;
    Click = Command(click);
  }

  public readonly string Name;
  public readonly string Label;

  /// <summary>Whether it shows what it holds after its name.</summary>
  public readonly bool ShowsValue;

  /// <summary>After the value, such as "px"; null for none.</summary>
  public readonly string Unit;

  /// <summary>Whether a color swatch leads its name.</summary>
  public readonly bool Swatch;

  /// <summary>The icon that leads its name; null for none.</summary>
  public readonly UiIconKind? Icon;

  /// <summary>Letters that lead its name, such as "Aa"; null for none.</summary>
  public readonly string Glyph;

  /// <summary>Whether a switch follows its name.</summary>
  public readonly bool Toggle;

  /// <summary>What it holds, as shown after its name.</summary>
  public readonly State<string> ValueText = new("");

  /// <summary>The value's color: the accent for a result of a formula.</summary>
  public readonly State<UiColor> ValueInk = new(Colors.Foreground);

  /// <summary>The name's and the icon's color: muted while there is nothing for it to act on.</summary>
  public readonly State<UiColor> Ink = new(Colors.Foreground);

  /// <summary>Its fill at rest: lit while what it opened is open, or while a value is dragged over it.</summary>
  public readonly State<UiColor> Fill = new(Rest);

  /// <summary>The swatch's color, opaque.</summary>
  public readonly State<Color> SwatchColor = new(Color.white);

  /// <summary>A gradient in the swatch instead of its color; null shows the color. Each one set is shown.</summary>
  public readonly State<OverlayResolvedGradient> SwatchGradient = new(null, new BothNull());

  /// <summary>The switch on, off, or null halfway.</summary>
  public readonly State<bool?> SwitchOn = new(false);

  /// <summary>Whether there is anything for the switch to act on.</summary>
  public readonly State<bool> Available = new(true);

  public Command Click { get; }

  /// <summary>What it shows after its name.</summary>
  public string Value => _value;

  /// <summary>The shown value came from a formula.</summary>
  public bool IsComputed { get; private set; }

  /// <summary>Lit while what the button opened is open.</summary>
  public bool Active => _active;

  /// <summary>0 nothing, 1 a dragged value can land here, -1 it can't.</summary>
  public int Drop => _drop;

  /// <summary>Goes up each time the shown value changes, which changes the button's width.</summary>
  public int Revision { get; private set; }

  /// <summary>
  /// Shows <paramref name="value"/> after the name; <paramref name="computed"/>
  /// marks a result of a formula, <paramref name="active"/> while what the
  /// button opened is open.
  /// </summary>
  public void Set(string value, bool computed, bool active)
  {
    value ??= "";
    if (value != _value)
    {
      _value = value;
      ValueText.Value = value;
      Revision++;
    }
    IsComputed = computed;
    ValueInk.Value = computed ? _computed : Colors.Foreground;
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
    SwitchOn.Value = on;
    Available.Value = available;
    Ink.Value = available ? Colors.Foreground : Colors.Muted;
  }

  /// <summary>The swatch's color, and a gradient shown instead of it (null for none).</summary>
  public void SetSwatch(Color color, OverlayResolvedGradient gradient)
  {
    color.a = 1;
    SwatchColor.Value = color;
    SwatchGradient.Value = gradient;
  }

  /// <summary>0 nothing, 1 a dragged value can land here, -1 it can't.</summary>
  public void SetDrop(int state)
  {
    if (_drop == state)
      return;
    _drop = state;
    Paint();
  }

  private void Paint() =>
    Fill.Value =
      _drop > 0 ? _accepts
      : _drop < 0 ? _refuses
      : _active ? _lit
      : Rest;

  // The swatch's gradient is one object filled again in place: only "none
  // again" is no change.
  private sealed class BothNull : IEqualityComparer<OverlayResolvedGradient>
  {
    public bool Equals(OverlayResolvedGradient a, OverlayResolvedGradient b) => a == null && b == null;

    public int GetHashCode(OverlayResolvedGradient gradient) => 0;
  }
}
