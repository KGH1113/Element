using Element.Presentation.Layout.ContentEditors.Shell.Dock;

namespace Element.Presentation.Layout.ContentEditors.Shell;

/// <summary>What the parts of a full-screen editor ask of the editor they make up.</summary>
internal interface IContentEditorHost
{
  /// <summary>The open editor's screen; null once it closed.</summary>
  OverlayEditorFrame Screen { get; }

  /// <summary>What the input dock shows: the editor tells it what is edited, and where a carried value would go.</summary>
  InputDockViewModel Dock { get; }

  /// <summary>Where the input dock sits, for what opens above it and for a carried value.</summary>
  InputDockPanel DockPanel { get; }

  /// <summary>The property column right of the preview, above the keypad.</summary>
  OverlayEditorInspector Inspector { get; }

  /// <summary>The element's scale on the overlay; sizes and numbers show screen pixels.</summary>
  float ElementScale { get; }

  /// <summary>A number or value is open in the dock, or a popover is.</summary>
  bool AnyOpen { get; }

  /// <summary>Closes the number or value being typed and the popovers.</summary>
  void CloseOpen();

  void Render();

  /// <summary>Esc: closes the picker or what is open, or with nothing open finishes like Done.</summary>
  void Escape();

  /// <summary>Done (<paramref name="commit"/>) or Cancel.</summary>
  void Close(bool commit, bool notify);
}
