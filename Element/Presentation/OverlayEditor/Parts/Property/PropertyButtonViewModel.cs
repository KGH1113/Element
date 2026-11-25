using Element.Presentation.OverlayEditor.Parts.Field;
using Element.Domain.Overlay.Variables;
using System;
using System.Collections.Generic;
using Element.Domain.Overlay.Values;
using Element.Presentation.Overlay.Elements;
using Element.Presentation.Ui.Graphics;
using Element.Presentation.Ui.Theme;
using ElementUI;
using UnityEngine;

namespace Element.Presentation.OverlayEditor.Parts.Property;

/// <summary>
/// One control in an editor's top bar: a button that says what it is and
/// what it holds, such as <c>● Fill</c> or <c>═ Border width 2 px</c>, and
/// what a click does. It stays lit while what it opened is open. A switch button shows
/// its state in a small switch after the name. The part it belongs to sets
/// what it shows; <see cref="PropertyButtonView"/> draws it.
/// </summary>
internal sealed class PropertyButtonViewModel : ViewModel
{
  private readonly UiColor _rest,
    _lit,
    _computed;
  private readonly State<bool> _computedValue = new(false);
  private readonly State<bool> _active = new(false);

  /// <param name="name">Names the drawn button: the bar finds it by this, and so do the checks.</param>
  /// <param name="label">What it is, such as "Fill".</param>
  /// <param name="click">What a click does: open its picker or number, or switch.</param>
  /// <param name="value">Whether it shows what it holds after its name, set with <see cref="Set"/>.</param>
  /// <param name="unit">After the value, such as "px".</param>
  /// <param name="swatch">A color swatch before the name, set with <see cref="SetSwatch"/>.</param>
  /// <param name="icon">An icon before the name.</param>
  /// <param name="glyph">Letters before the name, such as "Aa".</param>
  /// <param name="toggle">A switch after the name, set with <see cref="SetSwitch"/>.</param>
  /// <param name="opens">A click opens a tool in the editor's column (a list, a picker), marked with a chevron.</param>
  /// <param name="under">What it sits on: the editor's dimmed view when null, or a bar. Its fills are opaque, a little lighter than that.</param>
  public PropertyButtonViewModel(
    string name,
    string label,
    Action click,
    bool value = false,
    string unit = null,
    bool swatch = false,
    UiIconKind? icon = null,
    string glyph = null,
    bool toggle = false,
    bool opens = false,
    Color? under = null,
    OverlayValueType? field = null,
    string fieldGlyph = null
  )
  {
    Field = field;
    FieldGlyph = fieldGlyph;
    Name = name;
    Label = label;
    ShowsValue = value;
    Unit = value && !string.IsNullOrEmpty(unit) ? unit : null;
    Swatch = swatch;
    Icon = swatch ? null : icon;
    Glyph = swatch || icon.HasValue || string.IsNullOrEmpty(glyph) ? null : glyph;
    Toggle = toggle;
    Opens = opens && !toggle;
    // Opaque in every state, so what is behind never shows through; a state
    // tints the rest fill as a see-through accent over it would.
    Color rest = UiPalette.Wash(under ?? UiPalette.EditorDim, .07f),
      accent = UiPalette.SoftAccent;
    _rest = rest;
    _lit = Color.Lerp(rest, accent, .3f);
    _computed = accent;
    Fill = Computed(() => _active.Value ? _lit : _rest);
    ValueInk = Computed(() => _computedValue.Value ? _computed : Colors.Foreground);
    Ink = Computed(() => Available.Value ? Colors.Foreground : Colors.Muted);
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

  /// <summary>
  /// A field (칸): its type's mark leads the row and what it holds shows as
  /// chips (<see cref="ShowField(OverlayVariable,string,bool)"/>); null for a plain button.
  /// </summary>
  public readonly OverlayValueType? Field;

  /// <summary>Letters leading a field of a kind values don't have, as "Aa" for a font or "⊙" for an anchor.</summary>
  public readonly string FieldGlyph;

  /// <summary>Whether it is drawn as a field (칸).</summary>
  public bool IsField => Field.HasValue || FieldGlyph != null;

  /// <summary>What the field holds, as chips.</summary>
  public readonly State<FieldChip[]> Chips = new(Array.Empty<FieldChip>());

  /// <summary>What the field works out to, as "= 120 px"; empty when its chip already says it.</summary>
  public readonly State<string> Result = new("");

  /// <summary>Whether a click opens a tool in the editor's column, as a list or a picker does.</summary>
  public readonly bool Opens;

  /// <summary>What it holds, as shown after its name.</summary>
  public readonly State<string> ValueText = new("");

  /// <summary>The value's color: the accent for a result of a formula.</summary>
  public readonly Computed<UiColor> ValueInk;

  /// <summary>The name's and the icon's color: muted while there is nothing for it to act on.</summary>
  public readonly Computed<UiColor> Ink;

  /// <summary>Its fill at rest: lit while what it opened is open.</summary>
  public readonly Computed<UiColor> Fill;

  /// <summary>The swatch's color, opaque.</summary>
  public readonly State<Color> SwatchColor = new(Color.white);

  /// <summary>A gradient in the swatch instead of its color; null shows the color. Each one set is shown.</summary>
  public readonly State<OverlayResolvedGradient> SwatchGradient = new(null, new BothNull());

  /// <summary>The switch on, off, or null halfway.</summary>
  public readonly State<bool?> SwitchOn = new(false);

  /// <summary>Whether there is anything for the switch to act on.</summary>
  public readonly State<bool> Available = new(true);

  public Command Click { get; }

  /// <summary>What it shows after its name; for a field, what it works out to, or its chips' words.</summary>
  public string Value => IsField ? FieldText() : ValueText.Value;

  /// <summary>The shown value came from a formula.</summary>
  public bool IsComputed => IsField ? Result.Value.Length > 0 : _computedValue.Value;

  private string FieldText()
  {
    string result = Result.Value;
    if (result.Length > 0)
      return result.StartsWith("= ") ? result.Substring(2) : result;
    FieldChip[] chips = Chips.Value;
    if (chips.Length == 1)
      return chips[0].Label;
    System.Text.StringBuilder text = new();
    foreach (FieldChip chip in chips)
      text.Append(text.Length > 0 ? " " : "").Append(chip.Label);
    return text.ToString();
  }

  /// <summary>Lit while what the button opened is open.</summary>
  public bool Active => _active.Value;



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
    if (value != ValueText.Value)
    {
      ValueText.Value = value;
      Revision++;
    }
    _computedValue.Value = computed;
    _active.Value = active;
  }

  /// <summary>
  /// Shows what a field holds: <paramref name="variable"/>'s pieces as chips
  /// and, unless one chip already says it, what it works out to
  /// (<paramref name="result"/>, in its unit). <paramref name="active"/>
  /// lights the row while the field is open.
  /// </summary>
  public void ShowField(OverlayVariable variable, string result, bool active)
  {
    Chips.Value = FieldChips.Of(variable);
    bool plain =
      variable != null
      && variable.Count == 1
      && variable[0].Kind is not (OverlayPieceKind.Value or OverlayPieceKind.Condition or OverlayPieceKind.Group or OverlayPieceKind.Custom);
    Result.Value = plain || string.IsNullOrEmpty(result) ? "" : "= " + result;
    _active.Value = active;
  }

  /// <summary>Shows one chip as what a field holds, as a font's name or an anchor's point.</summary>
  public void ShowField(FieldChip chip, bool active) =>
    ShowField(chip == null ? Array.Empty<FieldChip>() : new[] { chip }, active);

  /// <summary>Shows <paramref name="chips"/> as what a field holds, as a color's.</summary>
  public void ShowField(FieldChip[] chips, bool active)
  {
    Chips.Value = chips ?? Array.Empty<FieldChip>();
    Result.Value = "";
    _active.Value = active;
    // A color field's first chip is its swatch, as the button showed it.
    if (Chips.Value.Length > 0 && Chips.Value[0].HasSwatch)
      SetSwatch(Chips.Value[0].Swatch, Chips.Value[0].Gradient);
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
  }

  /// <summary>The swatch's color, and a gradient shown instead of it (null for none).</summary>
  public void SetSwatch(Color color, OverlayResolvedGradient gradient)
  {
    color.a = 1;
    SwatchColor.Value = color;
    SwatchGradient.Value = gradient;
  }


  // The swatch's gradient is one object filled again in place: only "none
  // again" is no change.
  private sealed class BothNull : IEqualityComparer<OverlayResolvedGradient>
  {
    public bool Equals(OverlayResolvedGradient a, OverlayResolvedGradient b) => a == null && b == null;

    public int GetHashCode(OverlayResolvedGradient gradient) => 0;
  }
}
