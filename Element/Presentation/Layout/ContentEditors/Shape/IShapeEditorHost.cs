using Element.Configuration;
using Element.Presentation.Layout.ContentEditors.Shell;
using Element.Configuration.Overlay;

namespace Element.Presentation.Layout.ContentEditors.Shape;

/// <summary>What the parts of the shape editor ask of the editor they make up.</summary>
internal interface IShapeEditorHost
{
  /// <summary>The open editor's screen; null once it closed.</summary>
  OverlayEditorFrame Screen { get; }

  OverlayInputDock Dock { get; }

  OverlayEditorBar Bar { get; }

  /// <summary>The shape as edited so far. Undo puts another copy here, so read it afresh.</summary>
  OverlayShapeSettings Shape { get; }

  /// <summary>The element's scale on the overlay; the numbers show screen pixels.</summary>
  float ElementScale { get; }

  /// <summary>A number is open in the dock, or the grow popover is.</summary>
  bool AnyOpen { get; }

  /// <summary>A click on a number edits it in the dock; the same one again closes it.</summary>
  void Activate(string key);

  /// <summary>Closes the number being typed and the grow popover.</summary>
  void CloseEditing();

  void Render();

  /// <summary>Esc: closes what is open, or with nothing open finishes like Done.</summary>
  void Escape();

  /// <summary>Done (<paramref name="commit"/>) or Cancel.</summary>
  void Close(bool commit, bool notify);
}
