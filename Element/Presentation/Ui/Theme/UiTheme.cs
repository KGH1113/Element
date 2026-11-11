using System;
using UnityEngine;

namespace Element.Presentation.Ui.Theme;

/// <summary>The highlight color the player picked; Element's screens and the overlay draw their accent with it.</summary>
internal static class UiTheme
{
  public const string DefaultHighlight = "#BA712B";
  public static Color Accent { get; private set; } = new(186f / 255f, 113f / 255f, 43f / 255f, 1f);
  public static int Version { get; private set; }

  /// <summary>Raised after the highlight color actually changes.</summary>
  public static event Action Changed;

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
}
