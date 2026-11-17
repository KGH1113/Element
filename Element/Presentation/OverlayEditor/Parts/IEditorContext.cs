using Element.Presentation.OverlayEditor.Parts.Dock;
using Element.Presentation.OverlayEditor.Parts.EditorFrame;
using Element.Presentation.OverlayEditor.Parts.Inspector;

namespace Element.Presentation.OverlayEditor.Parts;

/// <summary>
/// What a full-screen editor's parts read from it: where its pieces are and
/// what is edited. What they ask it to do goes the other way, through
/// <see cref="IEditorActions"/>.
/// </summary>
internal interface IEditorContext
{
  /// <summary>The open editor's frame: the dim, the header and the panels; null once it closed.</summary>
  EditorFramePanel Frame { get; }

  /// <summary>What the input dock shows: a part says where a carried value would go.</summary>
  InputDockViewModel Dock { get; }

  /// <summary>Where the input dock sits, for what opens above it and for a carried value.</summary>
  InputDockPanel DockPanel { get; }

  /// <summary>The property column right of the preview, above the keypad.</summary>
  InspectorPanel Inspector { get; }

  /// <summary>The element's scale on the overlay; sizes and numbers show screen pixels.</summary>
  float ElementScale { get; }

  /// <summary>A number or value is open in the dock, or a popover is.</summary>
  bool AnyOpen { get; }
}
