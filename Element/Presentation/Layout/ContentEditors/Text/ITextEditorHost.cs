using Element.Presentation.Layout.ContentEditors.Shell;
using Element.Presentation.Overlay.Text;
using UnityEngine;

namespace Element.Presentation.Layout.ContentEditors.Text;

/// <summary>What the parts of the text editor ask of the editor they make up.</summary>
internal interface ITextEditorHost
{
  /// <summary>The open editor's screen; null once it closed.</summary>
  OverlayEditorFrame Screen { get; }

  OverlayInputDock Dock { get; }

  OverlayEditorBar Bar { get; }

  /// <summary>The text element being edited, for its fonts and where it sits.</summary>
  TextOverlayElement Element { get; }

  /// <summary>The element's scale on the overlay; sizes show screen pixels.</summary>
  float ElementScale { get; }

  /// <summary>A number or value is open in the dock, or a popover is.</summary>
  bool AnyPopover { get; }

  void Render();

  /// <summary>Closes the number or value being typed and the popovers.</summary>
  void ClosePopovers();

  /// <summary>The dimmed space around the text belongs to it; the bars, popovers and the picker don't.</summary>
  bool InTextArea(Vector2 screen);

  /// <summary>Esc: closes the picker, or finishes like Done.</summary>
  void Escape();

  /// <summary>Done (<paramref name="commit"/>) or Cancel.</summary>
  void Close(bool commit, bool notify);
}
