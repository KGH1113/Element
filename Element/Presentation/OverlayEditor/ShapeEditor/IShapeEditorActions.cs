using Element.Presentation.OverlayEditor.Parts;

namespace Element.Presentation.OverlayEditor.ShapeEditor;

/// <summary>What the shape editor's parts ask of it beyond any editor's.</summary>
internal interface IShapeEditorActions : IEditorActions
{
  /// <summary>A click on a number edits it in the dock; the same one again closes it.</summary>
  void Activate(string key);
}
