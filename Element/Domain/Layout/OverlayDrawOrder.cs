using System;
using System.Collections.Generic;
using Element.Configuration.Overlay;

namespace Element.Domain.Layout;

/// <summary>
/// Where each saved element is drawn, back to front: its place in the layout
/// list. Key viewers are elements of that list too, so the covers that cut
/// elements out of a viewer ask this which ones are in front of it. Notices a
/// changed list without allocating, so it can be asked every frame.
/// </summary>
internal sealed class OverlayDrawOrder
{
  private readonly Dictionary<string, int> _positions = new(StringComparer.Ordinal);
  private readonly List<OverlayLayoutSettings> _seen = new();
  private readonly List<bool> _enabled = new();

  /// <summary>
  /// Reads <paramref name="layouts"/> again; true when an entry was added,
  /// removed, moved or turned on or off since the last read.
  /// </summary>
  public bool Refresh(IReadOnlyList<OverlayLayoutSettings> layouts)
  {
    int count = layouts?.Count ?? 0;
    bool changed = count != _seen.Count;
    for (int i = 0; !changed && i < count; i++)
      changed = !ReferenceEquals(layouts[i], _seen[i]) || (layouts[i]?.Enabled ?? false) != _enabled[i];
    if (!changed)
      return false;
    _seen.Clear();
    _enabled.Clear();
    _positions.Clear();
    for (int i = 0; i < count; i++)
    {
      OverlayLayoutSettings layout = layouts[i];
      _seen.Add(layout);
      _enabled.Add(layout?.Enabled ?? false);
      // The first entry of an id wins, as the canvas and the key viewers find it.
      if (!string.IsNullOrEmpty(layout?.Id) && !_positions.ContainsKey(layout.Id))
        _positions.Add(layout.Id, i);
    }
    return true;
  }

  /// <summary>
  /// The place the element <paramref name="id"/> is drawn at: 0 is the back.
  /// -1 when the list lacks it. This is what "in front" means for covers
  /// (<see cref="KeyViewer.ViewerCoverRule.InFront"/>); a draw order other
  /// than the list's, such as one from layer groups, belongs here.
  /// </summary>
  public int Position(string id) => id != null && _positions.TryGetValue(id, out int position) ? position : -1;
}
