namespace Element.Domain.Elements;

// Where another mod's values sit in the dock: a heading in both languages,
// and the mod that added them.
internal sealed class OverlayValueGroup
{
  public readonly string Source;
  public readonly string KoreanName;
  public readonly string EnglishName;

  public OverlayValueGroup(string source, string koreanName, string englishName)
  {
    Source = source ?? string.Empty;
    KoreanName = koreanName ?? string.Empty;
    EnglishName = englishName ?? string.Empty;
  }
}
