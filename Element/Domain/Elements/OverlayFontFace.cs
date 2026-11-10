using System;
using Element.Configuration;

namespace Element.Domain.Elements;

/// <summary>
/// One font face the overlay can draw with: a font installed on this PC, or a
/// file in the mod's Fonts folder (<see cref="Imported"/>). A path of null is
/// an installed font known only by its names.
/// </summary>
internal sealed class OverlayFontFace
{
  public OverlayFontFace(string family, string style, string path, int faceIndex, bool imported)
  {
    Family = family ?? string.Empty;
    Style = string.IsNullOrWhiteSpace(style) ? OverlayFontKey.RegularStyle : style;
    Path = path;
    FaceIndex = faceIndex;
    Imported = imported;
    Key = OverlayFontKey.Create(Family, Style);
  }

  public string Family { get; }
  public string Style { get; }
  public string Path { get; }
  public int FaceIndex { get; }
  public bool Imported { get; }
  public string Key { get; }

  public bool Matches(string family, string style) =>
    string.Equals(Family, family, StringComparison.OrdinalIgnoreCase)
    && string.Equals(Style, style, StringComparison.OrdinalIgnoreCase);

  /// <summary>
  /// Sorting weight of a style name: Thin first, Black last, an italic right
  /// after its upright face.
  /// </summary>
  public static int StyleRank(string style)
  {
    string name = (style ?? string.Empty).Replace(" ", "").Replace("-", "").ToLowerInvariant();
    int weight =
      Has(name, "thin") || Has(name, "hairline") ? 100
      : Has(name, "extralight") || Has(name, "ultralight") ? 200
      : Has(name, "semibold") || Has(name, "demibold") ? 600
      : Has(name, "extrabold") || Has(name, "ultrabold") ? 800
      : Has(name, "light") ? 300
      : Has(name, "medium") ? 500
      : Has(name, "bold") ? 700
      : Has(name, "black") || Has(name, "heavy") ? 900
      : 400;
    return weight + (Has(name, "italic") || Has(name, "oblique") ? 1 : 0);
  }

  private static bool Has(string name, string part) => name.IndexOf(part, StringComparison.Ordinal) >= 0;
}
