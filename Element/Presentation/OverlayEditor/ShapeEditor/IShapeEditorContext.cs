using Element.Configuration.Overlay;
using Element.Presentation.OverlayEditor.Parts;

namespace Element.Presentation.OverlayEditor.ShapeEditor;

/// <summary>What the shape editor's parts read from it beyond any editor's.</summary>
internal interface IShapeEditorContext : IEditorContext
{
  /// <summary>The shape as edited so far. Undo puts another copy here, so read it afresh.</summary>
  OverlayShapeSettings Shape { get; }
}
