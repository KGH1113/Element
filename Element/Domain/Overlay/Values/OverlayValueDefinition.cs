namespace Element.Domain.Overlay.Values;

internal sealed class OverlayValueDefinition
{
  public readonly OverlayValueId Id;
  public readonly string Key;
  public readonly OverlayValueCategory Category;
  public readonly OverlayValueKind Kind;
  public readonly string KoreanName;
  public readonly string EnglishName;

  // Shown in the editor while the game has no live value.
  public readonly string Sample;
  public readonly double SampleNumber;

  // Another mod's heading; null for the built-in values.
  public readonly OverlayValueGroup Group;

  // False while the mod that adds the value is missing or turned off: texts
  // keep it, the overlay shows nothing there and the dock doesn't offer it.
  public readonly bool Available;

  public OverlayValueDefinition(
    OverlayValueId id,
    string key,
    OverlayValueCategory category,
    OverlayValueKind kind,
    string koreanName,
    string englishName,
    string sample,
    double sampleNumber,
    OverlayValueGroup group = null,
    bool available = true
  )
  {
    Id = id;
    Key = key;
    Category = category;
    Kind = kind;
    KoreanName = koreanName;
    EnglishName = englishName;
    Sample = sample;
    SampleNumber = sampleNumber;
    Group = group;
    Available = available;
  }

  public bool IsNumeric => Kind != OverlayValueKind.Text;

  public bool IsExtension => Category == OverlayValueCategory.Extension;
}
