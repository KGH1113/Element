using System.Collections.Generic;
using Element.Domain.Layout;

namespace Element.Application.KeyViewer;

/// <summary>
/// ElementNote's native key viewers, drawn above the game's picture: they
/// make way while the settings window or the layout editor is open, and where
/// overlay elements in front of them draw.
/// </summary>
internal interface INativeViewerLayer
{
  /// <summary>Shows or hides the viewers; the settings window covers the game.</summary>
  void SetVisible(bool visible);

  /// <summary>The editor's panels in screen pixels from the top left, with their corner radii.</summary>
  void SetEditorExclusions(IReadOnlyList<LayoutExclusion> screenTopLeftPixels);

  /// <summary>
  /// Whether the viewers' pages are on screen now (a play or the layout
  /// editor, with ElementNote connected); covers matter only then.
  /// </summary>
  bool Showing { get; }

  /// <summary>
  /// The parts of the viewer <paramref name="viewerId"/> (its layout id) that
  /// overlay elements in front of it draw over, which the viewer leaves out
  /// so the elements show. Screen pixels from the top left, not overlapping;
  /// empty shows the whole viewer. Kept until replaced, also while hidden.
  /// </summary>
  void SetCovers(string viewerId, IReadOnlyList<LayoutBounds> screenTopLeftPixels);
}
