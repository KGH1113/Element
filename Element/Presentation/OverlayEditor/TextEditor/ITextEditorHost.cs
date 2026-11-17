using Element.Presentation.Overlay.Text;
using Element.Presentation.OverlayEditor.Parts;
using UnityEngine;

namespace Element.Presentation.OverlayEditor.TextEditor;

/// <summary>What the parts of the text editor ask of it beyond any editor's host.</summary>
internal interface ITextEditorHost : IContentEditorHost
{
  /// <summary>The text element being edited, for its fonts and where it sits.</summary>
  TextOverlayElement Element { get; }

  /// <summary>The dimmed space around the text belongs to it; the bars, popovers and the picker don't.</summary>
  bool InTextArea(Vector2 screen);
}
