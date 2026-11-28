using Element.Application.Layout;
using Element.Domain.Layout;
using Element.Presentation.Overlay.Elements;

namespace Element.Presentation.Overlay.Covers;

/// <summary>What <see cref="KeyViewerCovers"/> last read of one overlay element.</summary>
internal sealed class ElementCoverState
{
  public ElementCoverState(IOverlayLayoutTarget target)
  {
    Target = target;
    Cover = target as IOverlayCover;
  }

  /// <summary>The canvas's element, by which its place in the canvas's list is matched.</summary>
  public IOverlayLayoutTarget Target { get; }

  /// <summary>Null for an element that can't say what it draws; such an element never covers.</summary>
  public IOverlayCover Cover { get; }

  /// <summary>The <see cref="IOverlayCover.CoverVersion"/> its bounds were read at; never read yet at first.</summary>
  public int Version { get; set; } = int.MinValue;

  /// <summary>Back to front, from the draw order.</summary>
  public int Position { get; set; } = -1;

  /// <summary>Whether it drew anything, and where, when last read.</summary>
  public bool Drawn { get; set; }

  public LayoutBounds Bounds { get; set; }

  /// <summary>Still in the canvas; used while the list is matched again.</summary>
  public bool Alive { get; set; }
}
