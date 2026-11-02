using System;
using System.Text;
using Element.Domain.Elements;
using Element.Presentation.Overlay.Shared;

namespace Element.Presentation.Overlay.Elements;

/// <summary>
/// Shows and calculates formulas. One value shows exactly as the game writes
/// it; anything calculated shows up to two decimals, or as m:ss when the
/// result is a time. Nothing here allocates, so texts updating on every hit
/// stay free of garbage.
/// </summary>
internal static class OverlayFormulaText
{
  /// <summary>Appends a value formula; nothing when a value it needs has no data yet.</summary>
  public static void Append(StringBuilder output, OverlayFormula formula, OverlayValues values, bool samples)
  {
    if (formula == null || formula.IsEmpty)
      return;
    if (formula.IsSingleValue(out OverlayValueId id))
    {
      OverlayTextComposer.AppendValue(output, id, values, samples);
      return;
    }
    IOverlayNumberSource source = samples ? values?.WithSamples : values;
    if (formula.TryEvaluate(source, out double result, out bool time))
      AppendResult(output, result, time);
  }

  public static StringBuilder AppendResult(StringBuilder output, double value, bool time)
  {
    if (!time)
      return output.AppendRounded(value, 2);
    long seconds = (long)Math.Round(value);
    if (seconds < 0)
    {
      output.Append('-');
      seconds = -seconds;
    }
    int whole = (int)Math.Min(int.MaxValue, seconds);
    return output.AppendClock(whole, whole);
  }

  /// <summary>
  /// A number field: its formula's result, or <paramref name="fallback"/> when
  /// it has no formula or a value it needs has no data.
  /// </summary>
  public static double Resolve(OverlayFormula formula, IOverlayNumberSource source, double fallback)
  {
    if (formula == null || formula.IsEmpty || !formula.TryEvaluate(source, out double result, out _))
      return fallback;
    return result;
  }

  /// <summary>Parsed once when settings change; null when the field has no formula.</summary>
  public static OverlayFormula Compile(string text)
  {
    if (string.IsNullOrEmpty(text))
      return null;
    OverlayFormula formula = OverlayFormula.Parse(text);
    return formula.IsEmpty ? null : formula;
  }
}
