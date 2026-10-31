using System.Collections.Generic;
using System.Text;
using Element.Configuration;
using Element.Domain.Elements;
using Element.Presentation.Overlay.Shared;
using UnityEngine;

namespace Element.Presentation.Overlay.Elements;

// Turns text runs into TMP rich text. Literal text never becomes markup, and
// each value is wrapped in a link so its characters can be found after layout.
internal static class OverlayTextComposer
{
  public const string ComboLink = "combo";
  public const string ValueLink = "v";

  public static ulong Mask(IReadOnlyList<OverlayTextRun> runs)
  {
    ulong mask = 0;
    if (runs == null)
      return mask;
    foreach (OverlayTextRun run in runs)
      if (run != null && run.IsValue && OverlayValueCatalog.TryGet(run.Value, out OverlayValueDefinition value))
        mask |= OverlayValueCatalog.Bit(value.Id);
    return mask;
  }

  /// <summary>Live overlay text. Values without data show their sample only when <paramref name="samples"/>.</summary>
  public static void Compose(
    StringBuilder output,
    IReadOnlyList<OverlayTextRun> runs,
    OverlayValues values,
    bool samples,
    Color highlight
  )
  {
    output.Clear();
    if (runs == null)
      return;
    foreach (OverlayTextRun run in runs)
    {
      if (run == null)
        continue;
      OpenStyle(output, run.Color, run.Size, highlight);
      if (run.IsValue)
      {
        if (OverlayValueCatalog.TryGet(run.Value, out OverlayValueDefinition value))
        {
          bool combo = value.Id == OverlayValueId.Combo;
          if (combo)
            output.Append("<link=\"").Append(ComboLink).Append("\">");
          AppendValue(output, value.Id, values, samples);
          if (combo)
            output.Append("</link>");
        }
      }
      else
        AppendLiteral(output, run.Text);
      CloseStyle(output, run.Size);
    }
  }

  public static Color Resolve(OverlayColorSettings color, Color highlight)
  {
    if (color == null)
      return Color.white;
    Color result = color.Highlight ? highlight : ParseHex(color.Hex);
    result.a = Mathf.Clamp01(color.Alpha);
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
  internal static void OpenStyle(StringBuilder output, OverlayColorSettings color, float size, Color highlight)
  {
    OpenColor(output, color, highlight);
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

  internal static void OpenColor(StringBuilder output, OverlayColorSettings color, Color highlight)
  {
    Color resolved = Resolve(color, highlight);
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
