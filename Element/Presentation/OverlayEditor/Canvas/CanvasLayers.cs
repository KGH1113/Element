using System.Collections.Generic;
using Element.Application.Layout;
using Element.Configuration.Overlay;
using Element.Domain.Layout;
using UnityEngine;
using static Element.Presentation.Ui.Theme.UiLocale;

namespace Element.Presentation.OverlayEditor.Canvas;

/// <summary>
/// What comes in front and what is grouped: grouping and ungrouping the
/// selection, moving it forward or back, and the moves, names and picks the
/// layers panel asks for. Each change is one undo step and stacks the canvas
/// again.
/// </summary>
internal sealed class CanvasLayers
{
  private readonly CanvasSession _session;
  private readonly CanvasSelection _selection;
  private readonly CanvasStatusLine _status;
  private readonly List<string> _ids = new();
  private readonly List<IOverlayLayoutTarget> _members = new();
  private readonly HashSet<string> _peeked = new();
  private List<OverlayLayoutSettings> _previewFrom;

  public CanvasLayers(CanvasSession session, CanvasSelection selection, CanvasStatusLine status)
  {
    _session = session;
    _selection = selection;
    _status = status;
  }

  /// <summary>The elements outlined while the pointer is over their layer card.</summary>
  public HashSet<string> Peeked => _peeked;

  private List<OverlayLayoutSettings> Layouts => _session.Host.LayoutSettings;

  /// <summary>Groups the selection; false when fewer than two are selected.</summary>
  public bool Group()
  {
    if (!_selection.Multi)
    {
      _status.ShowNote(L("두 개 이상 골라야 묶을 수 있어요.", "Pick two or more to group them."));
      return false;
    }
    if (_selection.Group != null)
    {
      _status.ShowNote(L("이미 한 묶음이에요.", "They are already one group."));
      return false;
    }
    SelectedIds();
    string group = LayoutGroups.Make(Layouts, _ids, NewName());
    if (group == null)
      return false;
    Changed();
    _selection.SelectGroup(group, _selection.Selected);
    _status.ShowNote(
      L(
        "묶었어요. 한 번 누르면 묶음 전체, 한 번 더 누르면 그 안의 요소를 골라요.",
        "Grouped. One click picks the group, another picks what's inside."
      )
    );
    return true;
  }

  /// <summary>Takes the selected group apart, or the group of the element picked inside it.</summary>
  public bool Ungroup()
  {
    string group = _selection.Group ?? _session.GroupOf(_selection.Primary);
    if (group == null)
    {
      _status.ShowNote(L("고른 것은 묶음이 아니에요.", "What's picked isn't a group."));
      return false;
    }
    _session.GroupFrames(group, _members);
    LayoutGroups.Dissolve(Layouts, group);
    Changed();
    _selection.Clear();
    foreach (IOverlayLayoutTarget member in _members)
      _selection.Targets.Add(member);
    _selection.Selected = _members.Count > 0 ? _session.IndexOf(_members[_members.Count - 1]) : -1;
    _status.ShowNote(L("묶음을 풀었어요. 요소들은 그 자리 그대로예요.", "Ungrouped. Everything stays where it was."));
    return true;
  }

  /// <summary>
  /// Moves the selection forward or back past the nearest element it
  /// overlaps, so each press shows a change; with <paramref name="toEnd"/>, all the way.
  /// </summary>
  public bool Arrange(bool forward, bool toEnd)
  {
    if (!_selection.HasSelection)
      return false;
    SelectedIds();
    bool moved = toEnd
      ? LayoutLayers.ToEnd(Layouts, _ids, forward)
      : LayoutLayers.Step(Layouts, _ids, forward, Overlaps);
    if (!moved)
    {
      _status.ShowNote(
        forward
          ? L("겹친 것 중 이미 맨 앞이에요.", "Already in front of what it overlaps.")
          : L("겹친 것 중 이미 맨 뒤예요.", "Already behind what it overlaps.")
      );
      return false;
    }
    Changed();
    return true;
  }

  /// <summary>A layer card let go: <paramref name="ids"/> just in front of <paramref name="behindId"/>, in <paramref name="group"/>.</summary>
  public void Move(IReadOnlyCollection<string> ids, string behindId, string group)
  {
    EndPreview();
    if (ids == null || ids.Count == 0)
      return;
    bool wholeGroup = IsWholeGroup(ids);
    LayoutLayers.PlaceInFrontOf(Layouts, ids, behindId);
    if (!wholeGroup)
      foreach (string id in ids)
        LayoutGroups.Assign(Layouts, id, group);
    Changed();
  }

  /// <summary>Shows the order a carried card would make; null ids bring the saved order back.</summary>
  public void Preview(IReadOnlyCollection<string> ids, string behindId)
  {
    if (ids == null)
    {
      EndPreview();
      return;
    }
    if (_previewFrom == null)
      _previewFrom = new List<OverlayLayoutSettings>(Layouts);
    else
    {
      Layouts.Clear();
      Layouts.AddRange(_previewFrom);
    }
    LayoutLayers.PlaceInFrontOf(Layouts, ids, behindId);
    _session.Host.ApplyOrder();
  }

  /// <summary>The order as it was before a carried card was shown somewhere else.</summary>
  public void EndPreview()
  {
    if (_previewFrom == null)
      return;
    Layouts.Clear();
    Layouts.AddRange(_previewFrom);
    _previewFrom = null;
    _session.Host.ApplyOrder();
  }

  public void Rename(string group, string name)
  {
    name = name?.Trim() ?? string.Empty;
    if (string.IsNullOrEmpty(group) || name.Length == 0)
      return;
    LayoutGroups.Rename(Layouts, group, name);
    _session.Commit();
  }

  /// <summary>A layer card was clicked: its element, or its whole group, is selected; with <paramref name="adding"/> it joins.</summary>
  public void Select(string id, string group, bool adding)
  {
    int index = -1;
    if (!string.IsNullOrEmpty(group))
    {
      _session.GroupFrames(group, _members);
      index = _members.Count > 0 ? _session.IndexOf(_members[_members.Count - 1]) : -1;
      if (adding)
        _selection.Toggle(index);
      else
        _selection.SelectGroup(group, index);
      return;
    }
    for (int i = 0; i < _session.Frames.Count; i++)
      if (_session.Frames[i].Target.Id == id)
        index = i;
    if (index < 0)
      return;
    if (adding && !_selection.Contains(_session.Frames[index].Target))
    {
      _selection.Targets.Add(_session.Frames[index].Target);
      _selection.Selected = index;
    }
    else if (adding)
      _selection.Targets.Remove(_session.Frames[index].Target);
    else
      _selection.SelectOnly(index);
  }

  public void Peek(IReadOnlyCollection<string> ids)
  {
    _peeked.Clear();
    if (ids != null)
      foreach (string id in ids)
        _peeked.Add(id);
  }

  /// <summary>
  /// After the order or the groups changed: groups stay together, a group
  /// of one is no group, the canvas restacks, and it is one undo step.
  /// </summary>
  public void Changed()
  {
    LayoutGroups.KeepTogether(Layouts);
    LayoutGroups.DropLonely(Layouts);
    _session.Host.ApplyOrder();
    _session.Commit();
  }

  // The next unused "Group n" in the player's language.
  private string NewName()
  {
    HashSet<string> used = new();
    foreach (OverlayLayoutSettings layout in Layouts)
      if (layout != null && !string.IsNullOrEmpty(layout.GroupName))
        used.Add(layout.GroupName);
    for (int n = 1; ; n++)
    {
      string name = L("묶음 ", "Group ") + n;
      if (!used.Contains(name))
        return name;
    }
  }

  private void SelectedIds()
  {
    _ids.Clear();
    foreach (IOverlayLayoutTarget target in _selection.Targets)
      _ids.Add(target.Id);
  }

  private bool IsWholeGroup(IReadOnlyCollection<string> ids)
  {
    string group = null;
    foreach (string id in ids)
    {
      string own = LayoutGroups.Of(Layouts, id);
      if (own == null || (group != null && own != group))
        return false;
      group = own;
    }
    return group != null && LayoutGroups.Members(Layouts, group, false).Count == ids.Count;
  }

  // Whether the element drawn as id overlaps the selection, where both show.
  private bool Overlaps(string id)
  {
    Rect selection = Union(_selection.Targets);
    foreach (CanvasFrame frame in _session.Frames)
      if (frame.Target.Id == id && CanvasSelection.Selectable(frame.Target))
        return Overlap(ScreenBounds(frame.Target), selection);
    return false;
  }

  private static Rect Union(List<IOverlayLayoutTarget> targets)
  {
    Rect union = default;
    bool any = false;
    foreach (IOverlayLayoutTarget target in targets)
    {
      Rect bounds = ScreenBounds(target);
      union = any
        ? Rect.MinMaxRect(
          Mathf.Min(union.xMin, bounds.xMin),
          Mathf.Min(union.yMin, bounds.yMin),
          Mathf.Max(union.xMax, bounds.xMax),
          Mathf.Max(union.yMax, bounds.yMax)
        )
        : bounds;
      any = true;
    }
    return union;
  }

  // Native viewers measure from their own window origin; move them into the editor's space.
  private static Rect ScreenBounds(IOverlayLayoutTarget target)
  {
    Rect bounds = target.SelectionBounds;
    bounds.position -= CanvasSession.OffsetOf(target);
    return bounds;
  }

  private static bool Overlap(Rect a, Rect b) =>
    a.width > 0 && a.height > 0 && b.width > 0 && b.height > 0 && a.Overlaps(b);
}
