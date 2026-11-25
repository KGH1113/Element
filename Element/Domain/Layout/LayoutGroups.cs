using System;
using System.Collections.Generic;
using Element.Configuration.Overlay;

namespace Element.Domain.Layout;

/// <summary>
/// Groups: elements that share <see cref="OverlayLayoutSettings.Group"/>. A
/// group is one level deep, its elements sit next to each other in the drawing
/// order, and a group left with one element is no group.
/// </summary>
internal static class LayoutGroups
{
  /// <summary>
  /// Groups <paramref name="ids"/> under a new id and <paramref name="name"/>,
  /// taking them out of the groups they were in. They gather where the
  /// front-most of them is. Returns the new group's id, or null for fewer than two.
  /// </summary>
  public static string Make(List<OverlayLayoutSettings> layouts, IReadOnlyCollection<string> ids, string name)
  {
    if (layouts == null || ids == null)
      return null;
    List<OverlayLayoutSettings> members = Find(layouts, ids);
    if (members.Count < 2)
      return null;
    string group = NewId(layouts);
    foreach (OverlayLayoutSettings member in members)
    {
      member.Group = group;
      member.GroupName = name ?? string.Empty;
    }
    KeepTogether(layouts);
    DropLonely(layouts);
    return group;
  }

  /// <summary>Takes every element out of <paramref name="group"/>; they keep their places.</summary>
  public static void Dissolve(List<OverlayLayoutSettings> layouts, string group)
  {
    if (layouts == null || string.IsNullOrEmpty(group))
      return;
    foreach (OverlayLayoutSettings layout in layouts)
      if (layout != null && layout.Group == group)
        Clear(layout);
  }

  public static void Rename(List<OverlayLayoutSettings> layouts, string group, string name)
  {
    if (layouts == null || string.IsNullOrEmpty(group))
      return;
    foreach (OverlayLayoutSettings layout in layouts)
      if (layout != null && layout.Group == group)
        layout.GroupName = name ?? string.Empty;
  }

  /// <summary>Puts <paramref name="id"/> in <paramref name="group"/>, or in no group for null.</summary>
  public static void Assign(List<OverlayLayoutSettings> layouts, string id, string group)
  {
    if (layouts == null)
      return;
    OverlayLayoutSettings layout = layouts.Find(candidate => candidate != null && candidate.Id == id);
    if (layout == null)
      return;
    if (string.IsNullOrEmpty(group))
    {
      Clear(layout);
      return;
    }
    OverlayLayoutSettings member = layouts.Find(candidate => candidate != null && candidate.Group == group);
    layout.Group = group;
    layout.GroupName = member?.GroupName ?? string.Empty;
  }

  /// <summary>The ids of <paramref name="group"/>'s elements, back to front.</summary>
  public static List<string> Members(
    IReadOnlyList<OverlayLayoutSettings> layouts,
    string group,
    bool enabledOnly = true
  )
  {
    List<string> members = new();
    if (layouts == null || string.IsNullOrEmpty(group))
      return members;
    foreach (OverlayLayoutSettings layout in layouts)
      if (layout != null && layout.Group == group && (!enabledOnly || layout.Enabled))
        members.Add(layout.Id);
    return members;
  }

  /// <summary>The group <paramref name="id"/> is in, or null.</summary>
  public static string Of(IReadOnlyList<OverlayLayoutSettings> layouts, string id)
  {
    if (layouts == null)
      return null;
    foreach (OverlayLayoutSettings layout in layouts)
      if (layout != null && layout.Id == id)
        return string.IsNullOrEmpty(layout.Group) ? null : layout.Group;
    return null;
  }

  /// <summary>
  /// Gathers each group's elements next to each other, where its front-most
  /// element is, keeping their order.
  /// </summary>
  public static void KeepTogether(List<OverlayLayoutSettings> layouts)
  {
    if (layouts == null || layouts.Count < 3)
      return;
    // Walk from the front so each group lands at its front-most element.
    List<OverlayLayoutSettings> fromFront = new(layouts.Count);
    HashSet<string> placed = new();
    for (int i = layouts.Count - 1; i >= 0; i--)
    {
      OverlayLayoutSettings layout = layouts[i];
      string group = layout?.Group;
      if (string.IsNullOrEmpty(group))
      {
        fromFront.Add(layout);
        continue;
      }
      if (!placed.Add(group))
        continue;
      for (int j = i; j >= 0; j--)
        if (layouts[j] != null && layouts[j].Group == group)
          fromFront.Add(layouts[j]);
    }
    fromFront.Reverse();
    layouts.Clear();
    layouts.AddRange(fromFront);
  }

  /// <summary>A group with fewer than two shown elements is no group any more.</summary>
  public static void DropLonely(List<OverlayLayoutSettings> layouts)
  {
    if (layouts == null)
      return;
    Dictionary<string, int> shown = new();
    foreach (OverlayLayoutSettings layout in layouts)
      if (layout != null && !string.IsNullOrEmpty(layout.Group) && layout.Enabled)
        shown[layout.Group] = shown.TryGetValue(layout.Group, out int count) ? count + 1 : 1;
    foreach (OverlayLayoutSettings layout in layouts)
      if (
        layout != null
        && !string.IsNullOrEmpty(layout.Group)
        && (!shown.TryGetValue(layout.Group, out int count) || count < 2)
      )
        Clear(layout);
  }

  /// <summary>A short id no element of <paramref name="layouts"/> uses as its group.</summary>
  public static string NewId(IReadOnlyList<OverlayLayoutSettings> layouts)
  {
    while (true)
    {
      string id = "group-" + Guid.NewGuid().ToString("N").Substring(0, 8);
      bool used = false;
      if (layouts != null)
        foreach (OverlayLayoutSettings layout in layouts)
          used |= layout != null && layout.Group == id;
      if (!used)
        return id;
    }
  }

  private static List<OverlayLayoutSettings> Find(List<OverlayLayoutSettings> layouts, IReadOnlyCollection<string> ids)
  {
    HashSet<string> wanted = new(ids);
    List<OverlayLayoutSettings> found = new();
    foreach (OverlayLayoutSettings layout in layouts)
      if (layout != null && wanted.Contains(layout.Id))
        found.Add(layout);
    return found;
  }

  private static void Clear(OverlayLayoutSettings layout)
  {
    layout.Group = string.Empty;
    layout.GroupName = string.Empty;
  }
}
