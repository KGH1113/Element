using UnityEngine;

namespace Element.Application.Input;

/// <summary>A settings viewport that temporarily takes the native editor gestures.</summary>
internal interface ITrackpadViewport
{
  bool Accepts(Vector2 normalizedPosition);
  void SuppressWheel();
  void Orbit(Vector2 normalizedDelta);
  void Rotate(float degrees);
  void Magnify(float delta);
}
