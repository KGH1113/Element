using System;
using System.Text;

namespace Element.Domain.Overlay.Values;

/// <summary>
/// Finds values by what players call them: a value's Korean or English name
/// and its key, its group's heading, and the name of its category or mod.
/// Case, spaces and marks such as '-' or '·' don't matter, so "xacc" finds
/// X-Accuracy and "kps" finds Keys per second.
/// </summary>
internal static class OverlayValueSearch
{
  /// <summary>Every name <paramref name="value"/> in <paramref name="folder"/> can be found by, folded; made once per value.</summary>
  public static string Index(OverlayValueFolder folder, OverlayValueDefinition value)
  {
    StringBuilder index = new(96);
    Append(index, value.KoreanName);
    Append(index, value.EnglishName);
    Append(index, value.Key);
    Append(index, folder.KoreanName);
    Append(index, folder.EnglishName);
    OverlayValueGroup group = value.Group;
    if (group != null)
    {
      Append(index, group.KoreanName);
      Append(index, group.EnglishName);
      Append(index, group.Source);
    }
    return index.ToString();
  }

  /// <summary>What was typed, folded the way <see cref="Index"/> folds names; empty when it holds nothing to look for.</summary>
  public static string Fold(string query)
  {
    if (string.IsNullOrEmpty(query))
      return string.Empty;
    StringBuilder folded = new(query.Length);
    AppendFolded(folded, query);
    return folded.ToString();
  }

  /// <summary>Whether a value whose <see cref="Index"/> is <paramref name="index"/> is found by <paramref name="folded"/>.</summary>
  public static bool Matches(string index, string folded) =>
    !string.IsNullOrEmpty(folded) && index != null && index.IndexOf(folded, StringComparison.Ordinal) >= 0;

  // Names are kept apart, so a search never matches across two of them.
  private static void Append(StringBuilder index, string name)
  {
    if (string.IsNullOrEmpty(name))
      return;
    index.Append('\n');
    AppendFolded(index, name);
  }

  private static void AppendFolded(StringBuilder to, string text)
  {
    foreach (char c in text)
      if (char.IsLetterOrDigit(c))
        to.Append(char.ToLowerInvariant(c));
  }
}
