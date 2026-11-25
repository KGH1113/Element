using System;
using System.Text;
using Element.Domain.Overlay.Values;
using Element.Domain.Overlay.Variables;
using Element.Presentation.Overlay.Values;

namespace Element.Presentation.Overlay.Text;

/// <summary>
/// Shows and calculates variables. Pieces side by side show one after the
/// other: one value exactly as the game writes it, words as typed, anything
/// calculated up to two decimals or as m:ss when the result is a time, a
/// condition as the answer it gives now. Nothing here allocates, so texts
/// updating on every hit stay free of garbage.
/// </summary>
internal static class OverlayFormulaText
{
  // Made values may use made values; this many deep is enough, and stops a loop.
  private const int MaxDepth = 16;

  /// <summary>Appends what a variable shows; nothing for a part whose values have no data yet.</summary>
  public static void Append(StringBuilder output, OverlayVariable formula, OverlayValues values, bool samples) =>
    Append(output, formula, values, samples, 0);

  private static void Append(
    StringBuilder output,
    OverlayVariable variable,
    OverlayValues values,
    bool samples,
    int depth
  )
  {
    if (variable == null || variable.IsEmpty || depth > MaxDepth)
      return;
    IOverlayNumberSource source = samples ? values?.WithSamples : values;
    for (int start = 0; start < variable.Count; )
    {
      int end = variable.SegmentEnd(start);
      if (end - start > 1)
      {
        if (OverlayVariableEvaluator.TryRange(variable, start, end, source, out double result, out bool time))
          AppendResult(output, result, time);
      }
      else
        AppendPiece(output, variable[start], values, samples, source, depth);
      start = end;
    }
  }

  private static void AppendPiece(
    StringBuilder output,
    OverlayPiece piece,
    OverlayValues values,
    bool samples,
    IOverlayNumberSource source,
    int depth
  )
  {
    switch (piece.Kind)
    {
      case OverlayPieceKind.Value:
        OverlayTextComposer.AppendValue(output, piece.Value, values, samples);
        break;
      case OverlayPieceKind.Number:
        if (piece.HasNumber)
          output.Append(piece.Text);
        break;
      case OverlayPieceKind.Text:
        output.Append(piece.Text);
        break;
      case OverlayPieceKind.Color:
        output.Append(piece.Text);
        break;
      case OverlayPieceKind.Bool:
        output.Append(piece.Bool ? Ui.Theme.UiLocale.L("참", "true") : Ui.Theme.UiLocale.L("거짓", "false"));
        break;
      case OverlayPieceKind.Condition:
        Append(output, OverlayVariableEvaluator.Chosen(piece, source), values, samples, depth + 1);
        break;
      case OverlayPieceKind.Group:
        Append(output, piece.Slots[0], values, samples, depth + 1);
        break;
      case OverlayPieceKind.Custom:
        if (OverlayCustomValues.TryGet(piece.Text, out OverlayVariable made))
          Append(output, made, values, samples, depth + 1);
        break;
    }
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
  public static double Resolve(OverlayVariable formula, IOverlayNumberSource source, double fallback)
  {
    if (formula == null || formula.IsEmpty || !formula.TryNumber(source, out double result, out _))
      return fallback;
    return result;
  }

  /// <summary>Parsed once when settings change; null when the field has no formula.</summary>
  public static OverlayVariable Compile(string text)
  {
    if (string.IsNullOrEmpty(text))
      return null;
    OverlayVariable formula = OverlayVariable.Parse(text);
    return formula.IsEmpty ? null : formula;
  }
}
