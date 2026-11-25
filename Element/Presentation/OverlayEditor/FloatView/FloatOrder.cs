using System.Collections.Generic;
using System.Linq;
using Element.Application.Layout;
using Element.Configuration.Overlay;
using Element.Domain.Layout;
using Element.Presentation.OverlayEditor.Canvas;

namespace Element.Presentation.OverlayEditor.FloatView;

/// <summary>
/// The adapter between the float view's height gesture and the saved draw
/// order: the layout list, back to front, where a group moves as one.
/// </summary>
internal sealed class FloatOrder
{
  private readonly CanvasSession _session;
  private readonly List<string> _ids = new();
  private readonly List<IOverlayLayoutTarget> _members = new();

  public FloatOrder(CanvasSession session) => _session = session;

  // Also used after history or Cancel restores the layout while the view is flat.
  public static void Apply(CanvasSession session) => session.Host.ApplyOrder();

  /// <summary>Every element that can be picked, back to front.</summary>
  public List<IOverlayLayoutTarget> Read() =>
    _session.Frames.Select(frame => frame.Target).Where(CanvasSelection.Selectable).OrderBy(_session.DrawIndex).ToList();

  /// <summary>
  /// One place toward the front (<paramref name="direction"/> 1) or the back
  /// (-1): the whole group when the element is in one. One undo step.
  /// </summary>
  public bool Move(IOverlayLayoutTarget target, int direction)
  {
    _ids.Clear();
    string group = _session.GroupOf(target);
    if (group != null)
    {
      _session.GroupFrames(group, _members);
      foreach (IOverlayLayoutTarget member in _members)
        _ids.Add(member.Id);
    }
    else
      _ids.Add(target.Id);
    List<OverlayLayoutSettings> layouts = _session.Host.LayoutSettings;
    if (!LayoutLayers.Shift(layouts, _ids, direction))
      return false;
    LayoutGroups.KeepTogether(layouts);
    Apply(_session);
    _session.Commit();
    return true;
  }
}
