using Element.Configuration.Overlay;
using Element.Presentation.Layout.ContentEditors.Shell;

namespace Element.Presentation.Layout.ContentEditors.Shape;

/// <summary>What the parts of the shape editor ask of it beyond any editor's host.</summary>
internal interface IShapeEditorHost : IContentEditorHost
{
  /// <summary>The shape as edited so far. Undo puts another copy here, so read it afresh.</summary>
  OverlayShapeSettings Shape { get; }

  /// <summary>A click on a number edits it in the dock; the same one again closes it.</summary>
  void Activate(string key);
}
