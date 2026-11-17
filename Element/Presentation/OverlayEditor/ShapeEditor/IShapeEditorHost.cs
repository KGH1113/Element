using Element.Configuration.Overlay;
using Element.Presentation.OverlayEditor.Parts;

namespace Element.Presentation.OverlayEditor.ShapeEditor;

/// <summary>What the parts of the shape editor ask of it beyond any editor's host.</summary>
internal interface IShapeEditorHost : IContentEditorHost
{
  /// <summary>The shape as edited so far. Undo puts another copy here, so read it afresh.</summary>
  OverlayShapeSettings Shape { get; }

  /// <summary>A click on a number edits it in the dock; the same one again closes it.</summary>
  void Activate(string key);
}
