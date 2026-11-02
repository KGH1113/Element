using System;
using Element.Configuration;
using Element.Domain.Elements;
using Element.Presentation.Settings;
using UnityEngine;

namespace Element.Presentation.Layout;

/// <summary>
/// The color picker's opacity box in the text and shape editors. It works like
/// W and H: a click types it in the input dock and a value can be dropped on
/// it. The picker's slider shows the result; moving the slider makes the
/// opacity a plain number again.
/// </summary>
internal sealed class OverlayOpacityField
{
  private readonly SettingsColorPicker _picker;
  private float _shown = -1;

  public OverlayOpacityField(SettingsUiFactory ui, SettingsColorPicker picker, Action activate, bool reduceMotion)
  {
    _picker = picker;
    picker.ExternalOpacity = true;
    Field = new OverlayFormulaField(ui, picker.OpacitySlot, "Opacity", "%", activate, reduceMotion);
  }

  public OverlayFormulaField Field { get; }

  public bool Visible => _picker.Visible;

  public bool Contains(Vector2 screen) => _picker.Visible && Field.Contains(screen);

  /// <summary>
  /// Shows a saved or typed opacity in percent and moves the slider to its
  /// result; <paramref name="fallback"/> is the plain opacity used while the
  /// formula has no result.
  /// </summary>
  public void Show(OverlayFormula formula, double percent, float fallback, bool active)
  {
    bool computed = !formula.IsEmpty && !formula.IsPlainNumber(out _);
    Field.Set(formula.IsEmpty ? "—" : OverlayFormulaField.Number(percent), computed, active);
    Rect slot = _picker.OpacitySlot.rect;
    Field.Place(slot.width - Field.Width, (slot.height - OverlayFormulaField.Height) * .5f);
    _shown =
      double.IsNaN(percent) || double.IsInfinity(percent)
        ? Mathf.Clamp01(fallback)
        : Mathf.Clamp01((float)percent / 100f);
    _picker.ShowOpacity(_shown);
  }

  /// <summary>
  /// A color change from the picker keeps a calculated opacity unless the
  /// opacity itself was moved.
  /// </summary>
  public OverlayColorSettings Keep(OverlayColorSettings before, OverlayColorSettings after)
  {
    if (
      before != null
      && after != null
      && !string.IsNullOrEmpty(before.AlphaFormula)
      && Mathf.Abs(after.Alpha - _shown) < .001f
    )
    {
      after.Alpha = before.Alpha;
      after.AlphaFormula = before.AlphaFormula;
    }
    return after;
  }

  /// <summary>An opacity as the dock reads it: the formula, or the plain percent.</summary>
  public static OverlayFormula Read(OverlayColorSettings color)
  {
    if (color != null && !string.IsNullOrEmpty(color.AlphaFormula))
      return OverlayFormula.Parse(color.AlphaFormula);
    return OverlayFormula.FromNumber(Mathf.Round(Mathf.Clamp01(color?.Alpha ?? 1) * 100));
  }

  /// <summary>
  /// A copy of <paramref name="color"/> with the typed opacity: a plain
  /// percent replaces any formula, a formula keeps the plain opacity underneath.
  /// </summary>
  public static OverlayColorSettings Write(OverlayColorSettings color, OverlayFormula formula)
  {
    OverlayColorSettings changed = (color ?? OverlayColorSettings.HighlightColor()).Clone();
    if (formula.IsPlainNumber(out double percent))
    {
      changed.Alpha = Mathf.Clamp01((float)percent / 100f);
      changed.AlphaFormula = string.Empty;
    }
    else
      changed.AlphaFormula = formula.Serialize();
    return changed;
  }

  /// <summary>A typed plain percent outside 0–100 was brought into range.</summary>
  public static bool OutOfRange(OverlayFormula typed) =>
    typed != null && typed.IsPlainNumber(out double percent) && (percent < 0 || percent > 100);
}
