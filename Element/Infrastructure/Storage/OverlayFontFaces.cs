using System;
using System.Collections.Generic;
using Element.Configuration;
using Element.Domain.Elements;

namespace Element.Infrastructure.Storage;

/// <summary>How font lists are ordered and how a saved font finds its face in one.</summary>
internal static class OverlayFontFaces
{
  // The exact face first; otherwise a family saved with its style in the
  // name, as some systems name weights ("Pretendard Bold" + "Regular").
  public static OverlayFontFace Match(IReadOnlyList<OverlayFontFace> faces, string family, string style)
  {
    foreach (OverlayFontFace face in faces)
      if (face.Matches(family, style))
        return face;
    string joined = family + " " + style;
    foreach (OverlayFontFace face in faces)
      if (
        string.Equals(face.Family + " " + face.Style, joined, StringComparison.OrdinalIgnoreCase)
        || (
          string.Equals(face.Family, joined, StringComparison.OrdinalIgnoreCase)
          && string.Equals(face.Style, OverlayFontKey.RegularStyle, StringComparison.OrdinalIgnoreCase)
        )
      )
        return face;
    return null;
  }

  public static void Sort(List<OverlayFontFace> faces) =>
    faces.Sort(
      (a, b) =>
      {
        int family = string.Compare(a.Family, b.Family, StringComparison.OrdinalIgnoreCase);
        if (family != 0)
          return family;
        int rank = OverlayFontFace.StyleRank(a.Style).CompareTo(OverlayFontFace.StyleRank(b.Style));
        return rank != 0 ? rank : string.Compare(a.Style, b.Style, StringComparison.OrdinalIgnoreCase);
      }
    );
}
