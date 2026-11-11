using System.Collections.Generic;
using Element.Domain.Layout;

namespace Element.Application.KeyViewer;

/// <summary>
/// ElementNote's native key viewers, drawn above the game's picture: they
/// make way while the settings window or the layout editor is open.
/// </summary>
internal interface INativeViewerLayer
{
  /// <summary>Shows or hides the viewers; the settings window covers the game.</summary>
  void SetVisible(bool visible);

  /// <summary>The editor's panels in screen pixels from the top left, with their corner radii.</summary>
  void SetEditorExclusions(IReadOnlyList<LayoutExclusion> screenTopLeftPixels);
}
