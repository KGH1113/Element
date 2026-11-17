using Element.Presentation.Overlay.Text;
using Element.Presentation.OverlayEditor.Parts;
using UnityEngine;

namespace Element.Presentation.OverlayEditor.TextEditor;

/// <summary>What the text editor's parts read from it beyond any editor's.</summary>
internal interface ITextEditorContext : IEditorContext
{
  /// <summary>The text element being edited, for its fonts and where it sits.</summary>
  TextOverlayElement Element { get; }

  /// <summary>The dimmed space around the text belongs to it; the bars, popovers and the picker don't.</summary>
  bool InTextArea(Vector2 screen);
}
