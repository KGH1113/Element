using System.Collections.Generic;
using System.Linq;
using Element.Application.Layout;
using Element.Presentation.Overlay.Elements;
using Element.Presentation.OverlayEditor.Canvas;

namespace Element.Presentation.OverlayEditor.FloatView;

/// <summary>The adapter between the editor's depth gesture and the stored draw order.</summary>
internal sealed class FloatOrder
{
  private readonly CanvasSession _session;

  public FloatOrder(CanvasSession session) => _session = session;

  // Also used after history or Cancel restores the layout while the view is flat.
  public static void Apply(CanvasSession session) => session.Host.ApplyDrawOrder();

  // On this branch each Unity layer owns its siblings. Native windows draw last.
  private static int Layer(IOverlayLayoutTarget target) =>
    target is OverlayElementTarget element ? element.Rect.parent.GetSiblingIndex() : int.MaxValue;

  private int Slot(IOverlayLayoutTarget target) =>
    _session.Host.LayoutSettings.FindIndex(settings => settings?.Id == target.Id);

  public List<IOverlayLayoutTarget> Read() =>
    _session
      .Frames.Select(frame => frame.Target)
      .Where(CanvasSelection.Selectable)
      .OrderBy(Layer)
      .ThenBy(Slot)
      .ToList();

  public bool Move(IOverlayLayoutTarget target, int direction)
  {
    var order = Read();
    int from = order.IndexOf(target);
    int to = from + direction;
    if (from < 0 || to < 0 || to >= order.Count || Layer(target) != Layer(order[to]))
      return false;
    // Native windows do not yet follow the Unity sibling order.
    if (!(target is OverlayElementTarget))
      return false;
    int a = Slot(target),
      b = Slot(order[to]);
    if (a < 0 || b < 0)
      return false;
    var layouts = _session.Host.LayoutSettings;
    (layouts[a], layouts[b]) = (layouts[b], layouts[a]);
    Apply(_session);
    _session.Commit();
    return true;
  }
}
