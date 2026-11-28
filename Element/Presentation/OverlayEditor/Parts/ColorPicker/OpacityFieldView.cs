using System;
using Element.Configuration.Overlay;
using Element.Domain.Overlay.Variables;
using Element.Presentation.OverlayEditor.Parts.Formula;
using Element.Presentation.Ui.ColorPicker;
using Element.Presentation.Ui.Widgets;
using UnityEngine;
using static Element.Presentation.Ui.Theme.UiLocale;

namespace Element.Presentation.OverlayEditor.Parts.ColorPicker;

/// <summary>
/// The color picker's opacity box in the text and shape editors. It works like
/// W and H: a click types it in the input dock and a value can be dropped on
/// it. The picker's slider shows the result; moving the slider makes the
/// opacity a plain number again.
/// </summary>
internal sealed class OpacityFieldView
{
  private readonly UiColorPicker _picker;
  private float _shown = -1;

  public OpacityFieldView(UiFactory ui, UiColorPicker picker, Action activate, bool reduceMotion)
  {
    _picker = picker;
    picker.ExternalOpacity = true;
    Field = new FormulaFieldView(ui, picker.OpacitySlot, "Opacity", "%", activate, reduceMotion);
  }

  public FormulaFieldView Field { get; }

  public bool Visible => _picker.Visible;

  public bool Contains(Vector2 screen) => _picker.Visible && Field.Contains(screen);

  /// <summary>
  /// Shows a saved or typed opacity in percent and moves the slider to its
  /// result; <paramref name="fallback"/> is the plain opacity used while the
  /// formula has no result.
  /// </summary>
  public void Show(OverlayVariable formula, double percent, float fallback, bool active)
  {
    bool computed = !formula.IsEmpty && !formula.IsPlainNumber(out _);
    Field.Set(formula.IsEmpty ? "—" : FormulaFieldView.Number(percent), computed, active);
    // Named where there is room for it: beside the color bar's own number.
    Field.Fill(_picker.GradientAvailable ? L("불투명도", "Opacity") : "");
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
  public static OverlayVariable Read(OverlayColorSettings color)
  {
    if (color != null && !string.IsNullOrEmpty(color.AlphaFormula))
      return OverlayVariable.Parse(color.AlphaFormula);
    return OverlayVariable.FromNumber(Mathf.Round(Mathf.Clamp01(color?.Alpha ?? 1) * 100));
  }

  /// <summary>
  /// A copy of <paramref name="color"/> with the typed opacity: a plain
  /// percent replaces any formula, a formula keeps the plain opacity underneath.
  /// </summary>
  public static OverlayColorSettings Write(OverlayColorSettings color, OverlayVariable formula)
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
  public static bool OutOfRange(OverlayVariable typed) =>
    typed != null && typed.IsPlainNumber(out double percent) && (percent < 0 || percent > 100);
}
