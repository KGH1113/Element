using System;
using UnityEngine;

namespace Element.Presentation.Overlay.Shared;

internal static class OverlayTheme
{
  public const string DefaultHighlight = "#BA712B";
  public static Color Accent { get; private set; } = new(186f / 255f, 113f / 255f, 43f / 255f, 1f);
  public static int Version { get; private set; }

  /// <summary>Raised after the highlight color actually changes.</summary>
  public static event Action Changed;
  public static readonly Color JudgementBase = new(0.8509804f, 0.345098f, 1f);

  public const double BpmColorMaximum = 8000d;

  public static Color AccentForeground
  {
    get
    {
      Color linear = Accent.linear;
      float luminance = linear.r * .2126f + linear.g * .7152f + linear.b * .0722f;
      return luminance > .179f ? new Color(.025f, .025f, .025f) : Color.white;
    }
  }

  public static void SetHighlight(string value)
  {
    if (!TryParseHighlight(value, out Color color))
      ColorUtility.TryParseHtmlString(DefaultHighlight, out color);
    if (Accent == color)
      return;
    Accent = color;
    Version++;
    Changed?.Invoke();
  }

  public static bool TryParseHighlight(string value, out Color color)
  {
    color = default;
    if (string.IsNullOrWhiteSpace(value))
      return false;
    string hex = value.Trim();
    if (!hex.StartsWith("#"))
      hex = "#" + hex;
    return hex.Length == 7 && ColorUtility.TryParseHtmlString(hex, out color);
  }

  public static Color ProgressColor(float progress)
  {
    return Color.Lerp(Color.white, Accent, Mathf.Clamp01(progress));
  }

  public static Color XAccuracyColor(float xAccuracy)
  {
    float amount = Mathf.InverseLerp(0.98f, 1f, xAccuracy);
    return Color.Lerp(Accent, Color.white, amount);
  }

  public static Color BpmColor(double bpm)
  {
    return Color.Lerp(Color.white, Accent, Mathf.Clamp01((float)(bpm / BpmColorMaximum)));
  }

  public static string ColorToHex(Color color)
  {
    return ColorUtility.ToHtmlStringRGB(color);
  }

  public static Color ComboColor(int _)
  {
    return Accent;
  }
}
