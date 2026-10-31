using UnityEngine;

namespace Element.Presentation.Layout;

internal interface IOverlayLayoutTarget
{
  string Id { get; }
  string TemplateId { get; }
  string Label { get; }

  // Physical screen pixels; x/y start at the TOP LEFT, including native viewers.
  Rect Bounds { get; }

  // Only the pixels occupied by the current content are selectable initially.
  // Once selected, Bounds remains the stable rectangle for moving and resizing.
  Rect SelectionBounds { get; }
  bool Available { get; }
  bool Enabled { get; }
  float EffectiveScale { get; }

  // Stage a pointer/keyboard edit in viewport pixels without changing saved placement.
  void SetBounds(Rect bounds);

  // Only explicit edit completion captures an anchor/offset. Resizing retains its anchor.
  // initialize captures a newly added instance even if its default is already centered.
  void CommitLayout(bool reselectAnchor, bool initialize = false);
  void SetEnabled(bool enabled);
  void SetPreview(bool preview);
  void RefreshLayout();
  void ResetLayout();
}

// Creates a visual-only sample using this overlay's own fonts, materials and
// composition. The returned root is centered and sized to its intrinsic content;
// the gallery can fit it into a tile without touching the live overlay.
internal interface IOverlayGalleryPreview
{
  GameObject CreateGalleryPreview(Transform parent);
}

// An external renderer owns this target's selection border. Unity must not
// draw a second copy underneath native/WebKit content.
internal interface IExternalLayoutOutline
{
  bool OwnsSelectionOutline { get; }
  void SetSelected(bool selected);
}

// Native AppKit content and Unity's drawable can have different vertical origins
// in a window. Evaluate native borders and pointers in the same host space.
internal interface IOverlayLayoutPointer
{
  Vector2 PointerInLayoutSpace(Vector2 fallback);
}
