namespace Element.Domain.Overlay.Values;

// Where another mod's values sit in the dock: a heading in both languages,
// and the mod that added them, by its id and by the name players know it by.
internal sealed class OverlayValueGroup
{
  public readonly string Source;
  public readonly string SourceName;
  public readonly string KoreanName;
  public readonly string EnglishName;

  /// <param name="source">The mod's id, the first part of its values' keys.</param>
  /// <param name="sourceName">The mod's name as players see it in the mod list; its id when unknown.</param>
  public OverlayValueGroup(string source, string koreanName, string englishName, string sourceName = null)
  {
    Source = source ?? string.Empty;
    SourceName = string.IsNullOrWhiteSpace(sourceName) ? Source : sourceName.Trim();
    KoreanName = koreanName ?? string.Empty;
    EnglishName = englishName ?? string.Empty;
  }
}
