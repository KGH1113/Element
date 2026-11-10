using System;
using System.Globalization;
using Element.Configuration;
using Element.Domain.Elements;
using ElementKit.Elements;

namespace Element.Presentation.Overlay.Mods;

/// <summary>
/// Reads and writes a mod element's saved properties. A property never saved
/// reads as the mod's default; a saved one outside today's limits or options
/// reads as the nearest it allows, and stays saved as it was until edited.
/// </summary>
internal static class ModPropertyValues
{
  public static double Number(NumberProperty property, OverlayPropertySetting saved)
  {
    double number =
      saved != null
      && double.TryParse(saved.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed)
      && !double.IsNaN(parsed)
      && !double.IsInfinity(parsed)
        ? parsed
        : property.Default;
    return Clamp(property, number);
  }

  public static double Clamp(NumberProperty property, double number) =>
    double.IsNaN(number) ? property.Default : Math.Max(property.Min, Math.Min(property.Max, number));

  /// <summary>What the dock edits: the formula, or the number as one.</summary>
  public static OverlayFormula NumberFormula(NumberProperty property, OverlayPropertySetting saved) =>
    string.IsNullOrEmpty(saved?.Formula)
      ? OverlayFormula.FromNumber(Math.Round(Number(property, saved) * 10) / 10)
      : OverlayFormula.Parse(saved.Formula);

  /// <summary>
  /// A plain number replaces any formula and is kept within limits; a
  /// formula keeps the number underneath. True when the number was clamped.
  /// </summary>
  public static bool WriteNumber(OverlayModSettings settings, NumberProperty property, OverlayFormula formula)
  {
    OverlayPropertySetting saved = settings.Get(property.Key);
    if (formula.IsPlainNumber(out double number))
    {
      double kept = Clamp(property, number);
      saved.Value = kept.ToString("R", CultureInfo.InvariantCulture);
      saved.Formula = string.Empty;
      return kept != number;
    }
    if (string.IsNullOrEmpty(saved.Value))
      saved.Value = property.Default.ToString("R", CultureInfo.InvariantCulture);
    saved.Formula = formula.Serialize();
    return false;
  }

  public static OverlayColorSettings Color(ColorProperty property, OverlayPropertySetting saved)
  {
    if (saved?.Color != null)
      return saved.Color;
    return property.StartsWithHighlight
      ? OverlayColorSettings.HighlightColor(property.DefaultOpacity)
      : OverlayColorSettings.Custom(property.DefaultColor, property.DefaultOpacity);
  }

  public static void WriteColor(OverlayModSettings settings, ColorProperty property, OverlayColorSettings color) =>
    settings.Get(property.Key).Color = color?.Clone();

  /// <summary>The picked option's index; the first for an option this version of the mod doesn't have.</summary>
  public static int Choice(ChoiceProperty property, OverlayPropertySetting saved)
  {
    if (saved != null)
      for (int i = 0; i < property.Options.Count; i++)
        if (property.Options[i].Key == saved.Value)
          return i;
    return 0;
  }

  public static void WriteChoice(OverlayModSettings settings, ChoiceProperty property, int index)
  {
    if (index >= 0 && index < property.Options.Count)
      settings.Get(property.Key).Value = property.Options[index].Key;
  }

  public static bool Toggle(ToggleProperty property, OverlayPropertySetting saved) =>
    saved == null || !bool.TryParse(saved.Value, out bool on) ? property.Default : on;

  public static void WriteToggle(OverlayModSettings settings, ToggleProperty property, bool on) =>
    settings.Get(property.Key).Value = on ? "true" : "false";
}
