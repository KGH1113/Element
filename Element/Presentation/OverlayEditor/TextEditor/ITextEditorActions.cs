using Element.Presentation.OverlayEditor.Parts;

namespace Element.Presentation.OverlayEditor.TextEditor;

/// <summary>What the text editor's parts ask of it beyond any editor's.</summary>
internal interface ITextEditorActions : IEditorActions
{
  /// <summary>
  /// Something done in the text (a click, a drag, a value dropped or typed
  /// over): the number or value being typed and the menus close, but the
  /// column's open tool stays and goes on with the new selection.
  /// </summary>
  void CloseTyping();
}
