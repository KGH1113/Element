using Element.Domain.Layout;

namespace Element.Domain.KeyViewer;

/// <summary>
/// When an overlay element hides part of a key viewer. Key viewers are drawn
/// outside the game, above its picture, so nothing Unity draws can really be
/// in front of one. Instead, the part of the viewer that an element in front
/// of it overlaps is not drawn, and the element underneath shows through.
/// </summary>
internal static class ViewerCoverRule
{
  /// <summary>
  /// Whether the element drawn at <paramref name="element"/> is in front of
  /// the viewer drawn at <paramref name="viewer"/>, both places from
  /// <see cref="OverlayDrawOrder.Position"/> (back to front). A place of -1
  /// is an element the layout list lacks, which is never in front.
  /// </summary>
  public static bool InFront(int element, int viewer) => element >= 0 && viewer >= 0 && element > viewer;

  /// <summary>Whether two screen areas share any pixels; empty areas share none.</summary>
  public static bool Overlap(LayoutBounds a, LayoutBounds b) =>
    a.Width > 0
    && a.Height > 0
    && b.Width > 0
    && b.Height > 0
    && a.X < b.Right
    && b.X < a.Right
    && a.Y < b.Bottom
    && b.Y < a.Bottom;

  /// <summary>
  /// Whether the element hides part of the viewer: it is in front of it and
  /// their areas overlap. Whether each is on and shown is the caller's to know.
  /// </summary>
  public static bool Covers(int element, LayoutBounds elementArea, int viewer, LayoutBounds viewerArea) =>
    InFront(element, viewer) && Overlap(elementArea, viewerArea);
}
