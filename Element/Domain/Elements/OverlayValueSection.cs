using System.Collections.Generic;

namespace Element.Domain.Elements;

// One heading of the dock and the values under it.
internal readonly struct OverlayValueSection
{
  public readonly OverlayValueCategory Category;
  public readonly OverlayValueGroup Group;
  public readonly IReadOnlyList<OverlayValueDefinition> Values;

  public OverlayValueSection(
    OverlayValueCategory category,
    OverlayValueGroup group,
    IReadOnlyList<OverlayValueDefinition> values
  )
  {
    Category = category;
    Group = group;
    Values = values;
  }
}
