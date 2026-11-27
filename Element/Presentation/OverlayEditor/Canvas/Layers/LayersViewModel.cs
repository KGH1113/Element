using System;
using System.Collections.Generic;
using Element.Application.Layout;
using Element.Configuration.Overlay;
using Element.Domain.Layout;
using Element.Domain.Overlay.Defaults;
using Element.Presentation.OverlayEditor.Kinds;
using ElementUI;
using UnityEngine;
using static Element.Presentation.Ui.Theme.UiLocale;

namespace Element.Presentation.OverlayEditor.Canvas.Layers;

/// <summary>
/// The layers panel: a card for each element, front-most at the top, a
/// group's elements under its card while it is open. A click picks what a
/// card stands for, a second click on a group's name renames it, and a card
/// carried up or down lands where the line shows, inside a group or not.
/// </summary>
internal sealed class LayersViewModel : ViewModel
{
  private static readonly UiColor[] Rails =
  {
    new Color(.42f, .66f, 1f),
    new Color(.77f, .61f, 1f),
    new Color(.37f, .82f, .63f),
    new Color(1f, .72f, .42f),
    new Color(1f, .56f, .69f),
    new Color(.5f, .84f, .91f),
  };

  private readonly CanvasSession _session;
  private readonly CanvasSelection _selection;
  private readonly ElementKinds _kinds;
  private readonly ICanvasActions _canvas;
  private readonly HashSet<string> _open = new();
  private readonly Dictionary<string, LayerRowViewModel> _byKey = new();
  private readonly List<LayerRowViewModel> _next = new();
  private readonly List<string> _ids = new();
  private string _lastPickKey;
  private float _lastPickTime = -1;
  private string _peeked;
  private int _stamp;
  private bool _carrying;

  /// <param name="floating">Whether Float view is on: its button in the panel is lit.</param>
  public LayersViewModel(
    CanvasSession session,
    CanvasSelection selection,
    ElementKinds kinds,
    ICanvasActions canvas,
    State<bool> floating
  )
  {
    _session = session;
    _selection = selection;
    _kinds = kinds;
    _canvas = canvas;
    Floating = floating;
    ToggleFloatView = Command(canvas.ToggleFloatView);
  }

  /// <summary>Float view: the elements stand apart in depth, in the order the cards show.</summary>
  public Command ToggleFloatView { get; }

  /// <summary>Float view is on.</summary>
  public readonly State<bool> Floating;

  /// <summary>The cards, front-most first.</summary>
  public LayerRowViewModel[] Rows = Array.Empty<LayerRowViewModel>();

  /// <summary>A carried card would land at the very back, just above the game.</summary>
  public bool InsertAtEnd;

  /// <summary>The group whose name is being typed; null when none is.</summary>
  public string RenamingGroup;

  public string RenameText = "";

  /// <summary>How tall the list may grow before it scrolls, set by the panel from the room above the toolbar.</summary>
  public float ListHeight = 400;

  /// <summary>The cards for the layout as it is now; only rebuilt when the order, the groups or the selection changed.</summary>
  public void Refresh()
  {
    // While a card is carried the canvas shows the order it would make; the
    // cards hold still under the pointer until it is let go.
    if (_carrying)
      return;
    int stamp = Stamp();
    if (stamp == _stamp && Rows.Length > 0)
      return;
    _stamp = stamp;
    List<OverlayLayoutSettings> layouts = _session.Host.LayoutSettings;
    OpenPickedGroup();
    string wholeGroup = _selection.Group;
    _next.Clear();
    HashSet<string> shownGroups = new();
    for (int i = layouts.Count - 1; i >= 0; i--)
    {
      OverlayLayoutSettings layout = layouts[i];
      if (!Shown(layout))
        continue;
      string group = string.IsNullOrEmpty(layout.Group) ? null : layout.Group;
      if (group == null)
      {
        _next.Add(ElementRow(layout, false));
        continue;
      }
      if (!shownGroups.Add(group))
        continue;
      List<string> members = LayoutGroups.Members(layouts, group);
      LayerRowViewModel header = Row("g:" + group);
      header.Id = null;
      header.Group = group;
      header.Header = true;
      header.Child = false;
      header.Name = GroupNames.Of(layouts, group);
      header.Count = members.Count.ToString();
      header.What = L("요소 " + members.Count + "개", members.Count == 1 ? "1 element" : members.Count + " elements");
      header.Open = _open.Contains(group);
      header.Selected = wholeGroup == group;
      header.Rail = RailOf(group);
      header.Ids = members.ToArray();
      _next.Add(header);
      if (!header.Open)
        continue;
      for (int j = members.Count - 1; j >= 0; j--)
      {
        OverlayLayoutSettings member = layouts.Find(l => l != null && l.Id == members[j]);
        if (Shown(member))
          _next.Add(ElementRow(member, true));
      }
    }
    foreach (LayerRowViewModel row in Rows)
      if (!_next.Contains(row))
      {
        _byKey.Remove(row.Key);
        row.Dispose();
      }
    Rows = _next.ToArray();
    ClearInsertion();
  }

  /// <summary>
  /// A card was clicked: it picks what the card stands for; with Shift or
  /// Cmd/Ctrl it joins the selection. A second click on a picked group's
  /// card renames it.
  /// </summary>
  public void Pick(LayerRowViewModel row, bool adding)
  {
    bool again = row.Key == _lastPickKey && Time.unscaledTime - _lastPickTime < .4f;
    _lastPickKey = row.Key;
    _lastPickTime = Time.unscaledTime;
    if (again && row.Header && !adding)
    {
      StartRename(row);
      return;
    }
    _canvas.SelectLayer(row.Id, row.Header ? row.Group : null, adding);
    _stamp = 0;
  }

  /// <summary>Opens or closes a group's card.</summary>
  public void Toggle(LayerRowViewModel row)
  {
    if (!row.Header)
      return;
    if (!_open.Remove(row.Group))
      _open.Add(row.Group);
    _stamp = 0;
    Refresh();
  }

  public void StartRename(LayerRowViewModel row)
  {
    RenamingGroup = row.Group;
    RenameText = row.Name;
  }

  /// <summary>The name field was left: Enter or a click elsewhere keeps a new name, Escape the old one.</summary>
  public void EndRename(bool cancelled)
  {
    string group = RenamingGroup;
    RenamingGroup = null;
    if (cancelled || group == null)
      return;
    string name = (RenameText ?? "").Trim();
    if (name.Length == 0 || name == GroupNames.Of(_session.Host.LayoutSettings, group))
      return;
    _canvas.RenameGroup(group, name);
    _stamp = 0;
    Refresh();
  }

  /// <summary>The pointer is over a card (null: over none): its elements are outlined on the canvas.</summary>
  public void Hover(LayerRowViewModel row)
  {
    string key = row?.Key;
    if (key == _peeked)
      return;
    _peeked = key;
    _canvas.PeekLayers(row?.Ids);
  }

  /// <summary>What a card carries when it is dragged.</summary>
  public LayerDrag DragOf(LayerRowViewModel row) =>
    row == null ? null : new LayerDrag(row.Key, row.Ids, row.Header ? row.Group : null, row.Name);

  /// <summary>
  /// The carried card is over the list, to land just above card
  /// <paramref name="before"/> (the end: just above the game). The line shows
  /// where, and the canvas shows the new order while it is held there.
  /// </summary>
  public void Over(LayerDrag drag, int before)
  {
    _carrying = true;
    Place(drag, before, out int line, out string group, out string behind);
    ClearInsertion();
    if (line >= Rows.Length)
      InsertAtEnd = true;
    else
    {
      Rows[line].InsertAbove = true;
      Rows[line].InsertInside = group != null;
    }
    _canvas.PreviewLayers(drag.Ids, behind);
    _peeked = drag.Key;
    _canvas.PeekLayers(drag.Ids);
  }

  /// <summary>The carried card left the list or the drag ended elsewhere: nothing moves.</summary>
  public void Leave()
  {
    _carrying = false;
    ClearInsertion();
    _canvas.PreviewLayers(null, null);
    Hover(null);
  }

  /// <summary>The card was let go above card <paramref name="before"/>: it moves there, one undo step.</summary>
  public void Drop(LayerDrag drag, int before)
  {
    Place(drag, before, out _, out string group, out string behind);
    _carrying = false;
    ClearInsertion();
    _canvas.PreviewLayers(null, null);
    _canvas.MoveLayers(drag.Ids, behind, group);
    _stamp = 0;
    Refresh();
  }

  // Where a card let go above row before lands: the row its line shows on,
  // the group it joins (null for none) and the element just behind it.
  private void Place(LayerDrag drag, int before, out int line, out string group, out string behind)
  {
    before = Mathf.Clamp(before, 0, Rows.Length);
    group = null;
    // A whole group never goes inside another: it lands above that group's card.
    if (drag.Group != null)
      while (before < Rows.Length && Rows[before].Child)
        before--;
    line = before;
    LayerRowViewModel next = before < Rows.Length ? Rows[before] : null;
    if (drag.Group == null && next != null && next.Child)
      group = next.Group;
    if (next == null)
    {
      behind = null;
      return;
    }
    // Above a group's card is in front of its front-most element.
    behind = next.Header ? next.Ids[next.Ids.Count - 1] : next.Id;
  }

  private void ClearInsertion()
  {
    InsertAtEnd = false;
    foreach (LayerRowViewModel row in Rows)
      row.InsertAbove = row.InsertInside = false;
  }

  private LayerRowViewModel ElementRow(OverlayLayoutSettings layout, bool child)
  {
    LayerRowViewModel row = Row("e:" + layout.Id);
    IOverlayLayoutTarget target = Target(layout.Id);
    row.Id = layout.Id;
    row.Group = string.IsNullOrEmpty(layout.Group) ? null : layout.Group;
    row.Header = false;
    row.Child = child;
    row.Name = target != null ? _kinds.Label(target) : layout.Id;
    row.What = target != null ? _kinds.KindName(target) : "";
    row.Count = "";
    row.Open = false;
    row.Selected =
      target != null && _selection.Contains(target) && (_selection.Group == null || row.Group != _selection.Group);
    row.Rail = row.Group != null ? RailOf(row.Group) : default;
    _ids.Clear();
    _ids.Add(layout.Id);
    row.Ids = _ids.ToArray();
    return row;
  }

  private LayerRowViewModel Row(string key)
  {
    if (!_byKey.TryGetValue(key, out LayerRowViewModel row))
    {
      row = new LayerRowViewModel(key);
      _byKey[key] = row;
    }
    return row;
  }

  // A group opens when one of its elements is picked on its own.
  private void OpenPickedGroup()
  {
    if (_selection.Count == 1 && _selection.Group == null)
    {
      string group = _session.GroupOf(_selection.Primary);
      if (group != null)
        _open.Add(group);
    }
  }

  private IOverlayLayoutTarget Target(string id)
  {
    foreach (CanvasFrame frame in _session.Frames)
      if (frame.Target.Id == id)
        return frame.Target;
    return null;
  }

  private static bool Shown(OverlayLayoutSettings layout) => layout != null && layout.Enabled;

  // The built-in groups keep their colors; others take one from their id.
  private static UiColor RailOf(string group)
  {
    if (group == OverlayElementDefaults.ProgressBarGroup)
      return Rails[0];
    if (group == OverlayElementDefaults.ComboGroup)
      return Rails[1];
    int hash = 0;
    foreach (char c in group)
      hash = hash * 31 + c;
    return Rails[2 + (int)((uint)hash % (uint)(Rails.Length - 2))];
  }

  // Changes when the order, the groups, the names, what is shown or what is picked change.
  private int Stamp()
  {
    unchecked
    {
      int stamp = 17;
      foreach (OverlayLayoutSettings layout in _session.Host.LayoutSettings)
      {
        if (layout == null)
          continue;
        stamp = stamp * 31 + layout.Id.GetHashCode();
        stamp = stamp * 31 + (layout.Group ?? "").GetHashCode();
        stamp = stamp * 31 + (layout.GroupName ?? "").GetHashCode();
        stamp = stamp * 31 + (layout.Enabled ? 1 : 0);
      }
      foreach (IOverlayLayoutTarget target in _selection.Targets)
        stamp = stamp * 31 + target.Id.GetHashCode();
      stamp = stamp * 31 + _session.Frames.Count;
      foreach (string group in _open)
        stamp = stamp * 31 + group.GetHashCode();
      return stamp == 0 ? 1 : stamp;
    }
  }

  protected override void OnDisposed()
  {
    foreach (LayerRowViewModel row in Rows)
      row.Dispose();
    _byKey.Clear();
  }

  /// <summary>The top of the list: what is in front.</summary>
  public static string Front => L("앞", "Front");

  /// <summary>The bottom of the list, beside the game: what is at the back.</summary>
  public static string Back => L("맨 뒤", "Back");
}
