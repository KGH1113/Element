using System;
using System.Collections.Generic;
using Element.Configuration.Overlay;

namespace Element.Domain.Layout;

/// <summary>
/// The drawing order. The layout list runs from the back to the front: the
/// last element draws over every other. A group's elements always sit next to
/// each other in it (<see cref="LayoutGroups"/>).
/// </summary>
internal static class LayoutLayers
{
  /// <summary>
  /// Where an element drew before layers: shapes at the back, then other mods'
  /// elements, then texts, with key viewers over everything.
  /// </summary>
  public static int LegacyLayer(string templateId)
  {
    if (templateId == OverlayElementKinds.Shape)
      return 0;
    if (OverlayElementKinds.IsMod(templateId))
      return 1;
    if (OverlayElementKinds.IsKeyViewer(templateId))
      return 3;
    return 2;
  }

  /// <summary>
  /// Puts layouts saved before layers in the order they were drawn, keeping
  /// the list's order within each kind.
  /// </summary>
  public static void SortByLegacyLayer(List<OverlayLayoutSettings> layouts)
  {
    if (layouts == null || layouts.Count < 2)
      return;
    List<(OverlayLayoutSettings Layout, int Layer, int Index)> keyed = new(layouts.Count);
    for (int i = 0; i < layouts.Count; i++)
      keyed.Add((layouts[i], layouts[i] == null ? 2 : LegacyLayer(layouts[i].TemplateId), i));
    keyed.Sort((a, b) => a.Layer != b.Layer ? a.Layer.CompareTo(b.Layer) : a.Index.CompareTo(b.Index));
    for (int i = 0; i < keyed.Count; i++)
      layouts[i] = keyed[i].Layout;
  }

  /// <summary>
  /// Moves <paramref name="ids"/>, keeping their own order, to sit just in
  /// front of <paramref name="behindId"/>; null puts them at the very back.
  /// When <paramref name="behindId"/> is one of them, they stay where they are.
  /// </summary>
  public static void PlaceInFrontOf(
    List<OverlayLayoutSettings> layouts,
    IReadOnlyCollection<string> ids,
    string behindId
  )
  {
    if (layouts == null || ids == null || ids.Count == 0)
      return;
    if (behindId != null && Contains(ids, behindId))
      return;
    List<OverlayLayoutSettings> moving = new();
    for (int i = layouts.Count - 1; i >= 0; i--)
      if (layouts[i] != null && Contains(ids, layouts[i].Id))
      {
        moving.Insert(0, layouts[i]);
        layouts.RemoveAt(i);
      }
    if (moving.Count == 0)
      return;
    int at = behindId == null ? 0 : IndexOf(layouts, behindId) + 1;
    if (at < 0 || at > layouts.Count)
      at = layouts.Count;
    layouts.InsertRange(at, moving);
  }

  /// <summary>
  /// Moves <paramref name="ids"/> one step to the front (<paramref name="forward"/>)
  /// or the back, past the nearest element or group whose content <paramref name="overlaps"/>
  /// theirs, so each step changes what is seen. Within a group, the step is among
  /// the group's own elements. False when nothing overlapping is that way.
  /// </summary>
  public static bool Step(
    List<OverlayLayoutSettings> layouts,
    IReadOnlyCollection<string> ids,
    bool forward,
    Func<string, bool> overlaps
  )
  {
    if (layouts == null || ids == null || ids.Count == 0)
      return false;
    List<List<OverlayLayoutSettings>> blocks = Blocks(layouts, ids, out int own, out string scope);
    if (own < 0)
      return false;
    int target = -1;
    if (forward)
    {
      for (int i = own + 1; i < blocks.Count && target < 0; i++)
        if (AnyOverlaps(blocks[i], overlaps))
          target = i;
    }
    else
      for (int i = own - 1; i >= 0 && target < 0; i--)
        if (AnyOverlaps(blocks[i], overlaps))
          target = i;
    if (target < 0)
      return false;
    List<OverlayLayoutSettings> block = blocks[own];
    blocks.RemoveAt(own);
    blocks.Insert(target, block);
    Rebuild(layouts, blocks, scope);
    return true;
  }

  /// <summary>Moves <paramref name="ids"/> to the very front or back, within their group when they are in one.</summary>
  public static bool ToEnd(List<OverlayLayoutSettings> layouts, IReadOnlyCollection<string> ids, bool front)
  {
    if (layouts == null || ids == null || ids.Count == 0)
      return false;
    List<List<OverlayLayoutSettings>> blocks = Blocks(layouts, ids, out int own, out string scope);
    int target = front ? blocks.Count - 1 : 0;
    if (own < 0 || own == target)
      return false;
    List<OverlayLayoutSettings> block = blocks[own];
    blocks.RemoveAt(own);
    blocks.Insert(target, block);
    Rebuild(layouts, blocks, scope);
    return true;
  }

  /// <summary>The ids in drawing order, back to front.</summary>
  public static List<string> Order(IReadOnlyList<OverlayLayoutSettings> layouts)
  {
    List<string> order = new(layouts?.Count ?? 0);
    if (layouts != null)
      foreach (OverlayLayoutSettings layout in layouts)
        if (layout != null)
          order.Add(layout.Id);
    return order;
  }

  public static int IndexOf(IReadOnlyList<OverlayLayoutSettings> layouts, string id)
  {
    if (layouts == null)
      return -1;
    for (int i = 0; i < layouts.Count; i++)
      if (layouts[i] != null && layouts[i].Id == id)
        return i;
    return -1;
  }

  // The steps a move takes: whole groups and lone elements, or the elements
  // of one group when the ids are inside it. own is the moving block's index.
  private static List<List<OverlayLayoutSettings>> Blocks(
    List<OverlayLayoutSettings> layouts,
    IReadOnlyCollection<string> ids,
    out int own,
    out string scope
  )
  {
    own = -1;
    scope = InnerGroup(layouts, ids);
    List<List<OverlayLayoutSettings>> blocks = new();
    List<OverlayLayoutSettings> moving = new();
    foreach (OverlayLayoutSettings layout in layouts)
    {
      if (layout == null)
        continue;
      if (scope != null && layout.Group != scope)
        continue;
      if (Contains(ids, layout.Id))
      {
        if (own < 0)
        {
          own = blocks.Count;
          blocks.Add(moving);
        }
        moving.Add(layout);
        continue;
      }
      string group = scope == null && !string.IsNullOrEmpty(layout.Group) ? layout.Group : null;
      if (
        group != null
        && blocks.Count > 0
        && blocks[blocks.Count - 1] != moving
        && SameGroup(blocks[blocks.Count - 1], group)
      )
        blocks[blocks.Count - 1].Add(layout);
      else
        blocks.Add(new List<OverlayLayoutSettings> { layout });
    }
    return blocks;
  }

  // The group the ids are inside of, when all of them are in one group and
  // they aren't the whole group; null to move among groups and lone elements.
  private static string InnerGroup(List<OverlayLayoutSettings> layouts, IReadOnlyCollection<string> ids)
  {
    string group = null;
    int members = 0;
    foreach (OverlayLayoutSettings layout in layouts)
    {
      if (layout == null || !Contains(ids, layout.Id))
        continue;
      if (string.IsNullOrEmpty(layout.Group) || (group != null && group != layout.Group))
        return null;
      group = layout.Group;
    }
    if (group == null)
      return null;
    foreach (OverlayLayoutSettings layout in layouts)
      if (layout != null && layout.Group == group)
        members++;
    return members > ids.Count ? group : null;
  }

  private static void Rebuild(
    List<OverlayLayoutSettings> layouts,
    List<List<OverlayLayoutSettings>> blocks,
    string scope
  )
  {
    List<OverlayLayoutSettings> ordered = new();
    foreach (List<OverlayLayoutSettings> block in blocks)
      ordered.AddRange(block);
    if (scope == null)
    {
      // Entries outside the blocks (nulls) keep the back.
      List<OverlayLayoutSettings> rest = layouts.FindAll(layout => layout == null);
      layouts.Clear();
      layouts.AddRange(rest);
      layouts.AddRange(ordered);
      return;
    }
    int next = 0;
    for (int i = 0; i < layouts.Count; i++)
      if (layouts[i] != null && layouts[i].Group == scope)
        layouts[i] = ordered[next++];
  }

  private static bool SameGroup(List<OverlayLayoutSettings> block, string group) =>
    block.Count > 0 && block[0].Group == group;

  private static bool AnyOverlaps(List<OverlayLayoutSettings> block, Func<string, bool> overlaps)
  {
    foreach (OverlayLayoutSettings layout in block)
      if (layout.Enabled && overlaps(layout.Id))
        return true;
    return false;
  }

  private static bool Contains(IReadOnlyCollection<string> ids, string id)
  {
    foreach (string candidate in ids)
      if (candidate == id)
        return true;
    return false;
  }
}
