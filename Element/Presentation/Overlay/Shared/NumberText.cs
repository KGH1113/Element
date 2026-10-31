using System;
using System.Text;
using TMPro;
using UnityEngine;

namespace Element.Presentation.Overlay.Shared;

/// <summary>
/// Appends invariant numbers without allocating. Overlay text changes on every hit,
/// and GC pauses also stop the async input thread.
/// </summary>
internal static class NumberText
{
  /// <summary>
  /// Assigns the buffer only when it differs from the current text, so an
  /// unchanged value costs no string. TMP's SetText(StringBuilder) is avoided:
  /// after it, the <c>text</c> getter returns the previous string from the second
  /// read on, which breaks layout bounds and mirrored copies that read it.
  /// </summary>
  public static void Assign(this TMP_Text text, StringBuilder builder)
  {
    string current = text.text;
    if (current != null && current.Length == builder.Length)
    {
      int index = 0;
      while (index < current.Length && current[index] == builder[index])
        index++;
      if (index == current.Length)
        return;
    }
    text.text = builder.ToString();
  }

  private static readonly double[] Scales = { 1d, 10d, 100d };

  public static StringBuilder AppendInt(this StringBuilder builder, long value)
  {
    if (value < 0)
    {
      builder.Append('-');
      // Negate in unsigned space so long.MinValue cannot overflow.
      return AppendDigits(builder, (ulong)(-(value + 1)) + 1);
    }
    return AppendDigits(builder, (ulong)value);
  }

  public static StringBuilder AppendTwoDigits(this StringBuilder builder, int value)
  {
    if (value >= 0 && value < 10)
      builder.Append('0');
    return builder.AppendInt(value);
  }

  /// <summary>Matches Math.Round(value, decimals).ToString(InvariantCulture) for 0–2 decimals.</summary>
  public static StringBuilder AppendRounded(this StringBuilder builder, double value, int decimals)
  {
    decimals = Math.Max(0, Math.Min(2, decimals));
    long scaled = Scaled(value, decimals);
    if (decimals <= 0)
      return builder.AppendInt(scaled);

    long scale = (long)Scales[decimals];
    if (scaled < 0)
    {
      builder.Append('-');
      scaled = -scaled;
    }
    builder.AppendInt(scaled / scale);
    long fraction = scaled % scale;
    if (fraction == 0)
      return builder;

    builder.Append('.');
    for (long digit = scale / 10; digit > 0 && fraction > 0; digit /= 10)
    {
      builder.Append((char)('0' + fraction / digit));
      fraction %= digit;
    }
    return builder;
  }

  /// <summary>Matches ColorUtility.ToHtmlStringRGB without allocating a string.</summary>
  public static StringBuilder AppendHexRgb(this StringBuilder builder, Color color)
  {
    AppendHexByte(builder, color.r);
    AppendHexByte(builder, color.g);
    return AppendHexByte(builder, color.b);
  }

  /// <summary>The value as displayed by AppendRounded, for change detection.</summary>
  public static long Scaled(double value, int decimals)
  {
    decimals = Math.Max(0, Math.Min(2, decimals));
    if (double.IsNaN(value) || double.IsInfinity(value))
      return 0;
    double scaled = Math.Round(value * Scales[decimals]);
    if (scaled >= long.MaxValue)
      return long.MaxValue;
    if (scaled <= long.MinValue)
      return long.MinValue;
    return (long)scaled;
  }

  private static StringBuilder AppendHexByte(StringBuilder builder, float channel)
  {
    int value = Mathf.Clamp(Mathf.RoundToInt(channel * 255f), 0, 255);
    return builder.Append(HexDigit(value >> 4)).Append(HexDigit(value & 15));
  }

  private static char HexDigit(int value) => (char)(value < 10 ? '0' + value : 'A' + value - 10);

  private static StringBuilder AppendDigits(StringBuilder builder, ulong value)
  {
    if (value == 0)
      return builder.Append('0');

    int start = builder.Length;
    while (value > 0)
    {
      builder.Append((char)('0' + (int)(value % 10)));
      value /= 10;
    }
    for (int left = start, right = builder.Length - 1; left < right; left++, right--)
    {
      char swap = builder[left];
      builder[left] = builder[right];
      builder[right] = swap;
    }
    return builder;
  }
}
