using System.Collections.Generic;
using Element.Configuration;

namespace Element.Domain.DmNote;

/// <summary>
/// How the key viewers on screen follow a layout list, such as a profile's.
/// Viewers the list lacks go, so another profile's viewers never stay on
/// screen or move into this list; viewers it has that aren't on screen yet
/// come. The two fixed gallery viewers always stay.
/// </summary>
internal static class NativeViewerLayoutSync
{
  /// <summary>"hand" or "foot" for a key viewer's template; null for any other element.</summary>
  public static string Kind(string templateId) =>
    templateId == OverlayElementKinds.HandKeyViewer ? "hand"
    : templateId == OverlayElementKinds.FootKeyViewer ? "foot"
    : null;

  /// <param name="current">Ids of the viewers there now.</param>
  /// <param name="fixedIds">The gallery viewers, which always stay.</param>
  /// <param name="layouts">The layout list to follow.</param>
  /// <param name="removed">Viewers the list doesn't have.</param>
  /// <param name="added">Viewers the list has that aren't there yet: id and kind.</param>
  public static void Plan(
    IEnumerable<string> current,
    ICollection<string> fixedIds,
    IReadOnlyList<OverlayLayoutSettings> layouts,
    List<string> removed,
    List<KeyValuePair<string, string>> added
  )
  {
    var listed = new HashSet<string>();
    foreach (OverlayLayoutSettings layout in layouts)
      if (layout != null && Kind(layout.TemplateId) != null)
        listed.Add(layout.Id);
    var present = new HashSet<string>();
    foreach (string id in current)
    {
      present.Add(id);
      if (!fixedIds.Contains(id) && !listed.Contains(id))
        removed.Add(id);
    }
    foreach (OverlayLayoutSettings layout in layouts)
    {
      string kind = layout == null ? null : Kind(layout.TemplateId);
      if (kind != null && present.Add(layout.Id))
        added.Add(new KeyValuePair<string, string>(layout.Id, kind));
    }
  }
}
