using System.Collections.Generic;
using System.Text;
using Element.Configuration;
using Element.Domain.Elements;
using Element.Presentation.Overlay.Shared;
using UnityEngine;

namespace Element.Presentation.Overlay.Elements;

// A run with its formulas parsed once, when the text's settings change.
internal sealed class OverlayCompiledRun
{
  public OverlayTextRun Run;
  public OverlayFormula Value;
  public OverlayFormula Size;
  public OverlayFormula Alpha;
}

// Turns text runs into TMP rich text. Literal text never becomes markup, and
// each value is wrapped in a link so its characters can be found after layout.
internal static class OverlayTextComposer
{
  public const string ComboLink = "combo";
  public const string ValueLink = "v";

  /// <summary>Parses every formula in <paramref name="runs"/>; returns the values they use as catalog bits.</summary>
  public static ulong Compile(IReadOnlyList<OverlayTextRun> runs, List<OverlayCompiledRun> output)
  {
    output.Clear();
    ulong mask = 0;
    if (runs == null)
      return mask;
    foreach (OverlayTextRun run in runs)
    {
      if (run == null)
        continue;
      OverlayCompiledRun compiled = new()
      {
        Run = run,
        Value = run.IsValue ? OverlayFormulaText.Compile(run.Value) : null,
        Size = OverlayFormulaText.Compile(run.SizeFormula),
        Alpha = OverlayFormulaText.Compile(run.Color?.AlphaFormula),
      };
      if (run.IsValue && compiled.Value == null)
        continue;
      mask |= (compiled.Value?.Mask ?? 0) | (compiled.Size?.Mask ?? 0) | (compiled.Alpha?.Mask ?? 0);
      output.Add(compiled);
    }
    return mask;
  }

  /// <summary>
  /// Live overlay text. Values without data show their sample only when
  /// <paramref name="samples"/>. Size formulas give pixels as drawn, so they are
  /// divided by <paramref name="elementScale"/>.
  /// </summary>
  public static void Compose(
    StringBuilder output,
    IReadOnlyList<OverlayCompiledRun> runs,
    OverlayValues values,
    bool samples,
    Color highlight,
    float elementScale
  )
  {
    output.Clear();
    if (runs == null)
      return;
    IOverlayNumberSource source = samples ? values?.WithSamples : values;
    foreach (OverlayCompiledRun compiled in runs)
    {
      OverlayTextRun run = compiled.Run;
      float size = Size(run.Size, compiled.Size, source, elementScale);
      OpenStyle(output, Resolve(run.Color, highlight, compiled.Alpha, source), size);
      if (compiled.Value != null)
      {
        bool combo = (compiled.Value.Mask & OverlayValueCatalog.Bit(OverlayValueId.Combo)) != 0;
        if (combo)
          output.Append("<link=\"").Append(ComboLink).Append("\">");
        OverlayFormulaText.Append(output, compiled.Value, values, samples);
        if (combo)
          output.Append("</link>");
      }
      else
        AppendLiteral(output, run.Text);
      CloseStyle(output, size);
    }
  }

  /// <summary>A run's size in reference pixels; a size formula's pixels are as drawn.</summary>
  public static float Size(float size, OverlayFormula formula, IOverlayNumberSource source, float elementScale)
  {
    if (formula == null)
      return size;
    double pixels = OverlayFormulaText.Resolve(formula, source, double.NaN);
    if (double.IsNaN(pixels))
      return size;
    return Mathf.Clamp((float)pixels, 1, 1000) / Mathf.Max(.01f, elementScale);
  }

  public static Color Resolve(OverlayColorSettings color, Color highlight)
  {
    if (color == null)
      return Color.white;
    Color result = color.Highlight ? highlight : ParseHex(color.Hex);
    result.a = Mathf.Clamp01(color.Alpha);
    return result;
  }

  /// <summary>The color with its opacity formula (percent) applied when it has one.</summary>
  public static Color Resolve(
    OverlayColorSettings color,
    Color highlight,
    OverlayFormula alpha,
    IOverlayNumberSource source
  )
  {
    Color result = Resolve(color, highlight);
    if (alpha != null)
    {
      double percent = OverlayFormulaText.Resolve(alpha, source, double.NaN);
      if (!double.IsNaN(percent))
        result.a = Mathf.Clamp01((float)percent / 100f);
    }
    return result;
  }

  public static Color ParseHex(string hex)
  {
    if (!string.IsNullOrEmpty(hex))
    {
      string value = hex[0] == '#' ? hex : "#" + hex;
      if (value.Length == 7 && ColorUtility.TryParseHtmlString(value, out Color color))
        return color;
    }
    return Color.white;
  }

  internal static void AppendValue(StringBuilder output, OverlayValueId id, OverlayValues values, bool samples)
  {
    if (values != null && values.TryGet(id, out StringBuilder live) && live.Length > 0)
      output.Append(live);
    else if (samples)
      OverlayValues.AppendSample(output, id);
  }

  /// <summary>Opens a run's color and, unless it is the base size, its size in reference pixels.</summary>
  internal static void OpenStyle(StringBuilder output, Color color, float size)
  {
    OpenColor(output, color);
    if (HasSize(size))
      output.Append("<size=").AppendRounded(size, 2).Append('>');
  }

  internal static void CloseStyle(StringBuilder output, float size)
  {
    if (HasSize(size))
      output.Append("</size>");
    output.Append("</color>");
  }

  private static bool HasSize(float size) =>
    size > 0 && Mathf.Abs(size - Domain.Elements.OverlayElementDefaults.TextSize) > .005f;

  internal static void OpenColor(StringBuilder output, Color resolved)
  {
    output.Append("<color=#").AppendHexRgb(resolved);
    int alpha = Mathf.Clamp(Mathf.RoundToInt(resolved.a * 255), 0, 255);
    const string digits = "0123456789ABCDEF";
    output.Append(digits[alpha >> 4]).Append(digits[alpha & 15]).Append('>');
  }

  // A lone '<' inside noparse cannot open a tag, so typed text never turns into markup.
  internal static void AppendLiteral(StringBuilder output, string text)
  {
    if (string.IsNullOrEmpty(text))
      return;
    foreach (char c in text)
      if (c == '<')
        output.Append("<noparse><</noparse>");
      else
        output.Append(c);
  }
}
