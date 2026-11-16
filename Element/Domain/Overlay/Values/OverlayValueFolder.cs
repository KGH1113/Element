using System.Collections.Generic;

namespace Element.Domain.Overlay.Values;

/// <summary>
/// One place in the dock's list of values: a built-in category, or
/// everything one mod adds, however many groups it made. Its sections are
/// its groups in the order they arrived; a category has one.
/// </summary>
internal sealed class OverlayValueFolder
{
  public OverlayValueFolder(
    OverlayValueCategory category,
    string source,
    string koreanName,
    string englishName,
    IReadOnlyList<OverlayValueSection> sections
  )
  {
    Category = category;
    Source = source;
    KoreanName = koreanName ?? string.Empty;
    EnglishName = englishName ?? string.Empty;
    Sections = sections;
    int count = 0;
    foreach (OverlayValueSection section in sections)
      count += section.Values.Count;
    Count = count;
    Key = source == null ? category.ToString() : "mod:" + source;
  }

  /// <summary>The built-in category; <see cref="OverlayValueCategory.Extension"/> for a mod.</summary>
  public readonly OverlayValueCategory Category;

  /// <summary>The mod's id; null for a built-in category.</summary>
  public readonly string Source;

  public readonly string KoreanName;
  public readonly string EnglishName;

  public readonly IReadOnlyList<OverlayValueSection> Sections;

  /// <summary>How many values it holds, across its groups.</summary>
  public readonly int Count;

  /// <summary>Names it the same way from one opening to the next: "Judgement", or "mod:TUFOverlay".</summary>
  public readonly string Key;

  public bool IsMod => Source != null;
}
