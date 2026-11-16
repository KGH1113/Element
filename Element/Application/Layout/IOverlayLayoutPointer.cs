using UnityEngine;

namespace Element.Application.Layout;

// Native AppKit content and Unity's drawable can have different vertical origins
// in a window. Evaluate native borders and pointers in the same host space.
internal interface IOverlayLayoutPointer
{
  Vector2 PointerInLayoutSpace(Vector2 fallback);
}
