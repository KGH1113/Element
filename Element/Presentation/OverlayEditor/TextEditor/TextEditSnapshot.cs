using Element.Domain.Overlay.Text;

namespace Element.Presentation.OverlayEditor.TextEditor;

/// <summary>A text editor's undo step: the text and where the caret and selection were.</summary>
internal readonly struct TextEditSnapshot
{
  public readonly OverlayTextDocument Document;
  public readonly int Caret;
  public readonly int Anchor;

  public TextEditSnapshot(OverlayTextDocument document, int caret, int anchor)
  {
    Document = document;
    Caret = caret;
    Anchor = anchor;
  }
}
