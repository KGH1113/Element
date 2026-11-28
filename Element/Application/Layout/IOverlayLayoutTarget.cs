using Element.Configuration.Overlay;
using UnityEngine;

namespace Element.Application.Layout;

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

  // Saves where it is now against the given screen point, as a group's
  // elements all do so they stay together when the screen size changes. A
  // target that can't hold that point picks its own nearest.
  void CommitLayout(Anchor9 anchor);
  void SetEnabled(bool enabled);
  void SetPreview(bool preview);
  void RefreshLayout();
  void ResetLayout();
}
