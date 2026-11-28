using Element.Presentation.OverlayEditor.Parts;

namespace Element.Presentation.OverlayEditor.ShapeEditor;

/// <summary>What the shape editor's parts ask of it beyond any editor's.</summary>
internal interface IShapeEditorActions : IEditorActions
{
  /// <summary>A click on a number edits it in the dock; the same one again closes it.</summary>
  void Activate(string key);

  /// <summary>
  /// Enter: what is open is done. A color puts its picker away; a number is
  /// finished, from however deep inside it, and refused while it doesn't fit.
  /// </summary>
  void Finish();
}
