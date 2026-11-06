using System;

namespace Element.Configuration;

// A saved font names one face by family and style, such as "Pretendard|Bold".
// The same layout then finds it on another PC whether the font is installed
// there or sits in the Fonts folder. Empty means the overlay's default font.
public static class OverlayFontKey
{
  private const char Separator = '|';
  public const string RegularStyle = "Regular";

  public static string Create(string family, string style)
  {
    family = Clean(family);
    if (family.Length == 0)
      return string.Empty;
    style = Clean(style);
    return family + Separator + (style.Length == 0 ? RegularStyle : style);
  }

  public static bool TryParse(string key, out string family, out string style)
  {
    family = style = string.Empty;
    if (string.IsNullOrEmpty(key))
      return false;
    int split = key.LastIndexOf(Separator);
    family = Clean(split < 0 ? key : key.Substring(0, split));
    style = split < 0 ? RegularStyle : Clean(key.Substring(split + 1));
    if (style.Length == 0)
      style = RegularStyle;
    return family.Length > 0;
  }

  /// <summary>The saved form of <paramref name="key"/>, or empty when it names no font.</summary>
  public static string Normalize(string key) =>
    TryParse(key, out string family, out string style) ? Create(family, style) : string.Empty;

  public static bool Same(string a, string b) =>
    string.Equals(Normalize(a), Normalize(b), StringComparison.OrdinalIgnoreCase);

  // A separator inside a name would split it in the wrong place on loading.
  private static string Clean(string name) => (name ?? string.Empty).Replace(Separator, ' ').Trim();
}
