using Element.Domain.KeyViewer;
using Element.Domain.Layout;

namespace Element.Presentation.Overlay.Elements;

/// <summary>
/// What an overlay element draws, as the key viewers behind it need it: the
/// viewers are drawn above the game, so each leaves out the pixels of the
/// elements in front of it. Every kind of element marks its own pixels here,
/// so the covers never ask which kind an element is.
/// </summary>
internal interface IOverlayCover
{
  /// <summary>Its layout id, which places it in the draw order.</summary>
  string Id { get; }

  /// <summary>
  /// Counts up whenever what it draws, where, or whether it shows may have
  /// changed; the viewers behind it take its pixels again only then.
  /// </summary>
  int CoverVersion { get; }

  /// <summary>
  /// The screen pixels (top left origin) everything it draws lies within;
  /// false while it draws nothing: off, hidden, empty or fully transparent.
  /// </summary>
  bool TryGetCoverBounds(out LayoutBounds screenTopLeft);

  /// <summary>Marks the pixels it draws inside the clip of <paramref name="runs"/>.</summary>
  void Rasterize(ViewerCoverRuns runs);
}
