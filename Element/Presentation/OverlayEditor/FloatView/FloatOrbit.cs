using UnityEngine;

namespace Element.Presentation.OverlayEditor.FloatView;

internal sealed class FloatOrbit
{
  private const float InitialYaw = 16,
    InitialPitch = 48;
  private float _yaw = InitialYaw,
    _pitch = InitialPitch,
    _zoom = 1;
  private Vector2 _velocity;
  private float _zoomVelocity,
    _lastZoom = -1,
    _lastDrag = -1;
  private bool _resetting,
    _twisting;

  public float Yaw => _yaw;
  public float Pitch => Rubber(_pitch, 15, 75, 9);
  public float Zoom => Rubber(_zoom, .55f, 1.9f, .24f);

  private static float Rubber(float value, float min, float max, float limit)
  {
    float edge = Mathf.Clamp(value, min, max);
    float overflow = value - edge;
    return edge + overflow / (1 + Mathf.Abs(overflow) / limit);
  }

  public void Grab()
  {
    _resetting = false;
    _twisting = false;
    _velocity = Vector2.zero;
  }

  public void Orbit(Vector2 normalized, bool trackVelocity)
  {
    _resetting = false;
    Vector2 delta = new(normalized.x * 200, normalized.y * 100);
    _yaw += delta.x;
    _pitch += delta.y;
    _velocity = trackVelocity
      ? Vector2.ClampMagnitude(delta / Mathf.Max(.008f, Time.unscaledDeltaTime), 240)
      : Vector2.zero;
    _lastDrag = Time.unscaledTime;
  }

  public void Rotate(float degrees)
  {
    if (degrees == 0)
      return;
    _resetting = false;
    _twisting = true;
    _yaw += degrees;
    _velocity.x = Mathf.Clamp(degrees / Mathf.Max(.016f, Time.unscaledDeltaTime), -240, 240);
    _lastDrag = Time.unscaledTime;
  }

  public void Magnify(float amount)
  {
    if (amount == 0)
      return;
    _resetting = false;
    float delta = Zoom * Mathf.Clamp(amount, -.5f, .5f);
    _zoom += delta;
    _zoomVelocity = Mathf.Clamp(delta / Mathf.Max(.016f, Time.unscaledDeltaTime), -1.5f, 1.5f);
    _lastZoom = Time.unscaledTime;
  }

  public void Reset()
  {
    _resetting = true;
    _yaw = InitialYaw + Mathf.DeltaAngle(InitialYaw, _yaw);
    _velocity = Vector2.zero;
    _zoomVelocity = 0;
  }

  public void Tick(float dt, bool held, bool reduceMotion)
  {
    float settle = reduceMotion ? 1 : 1 - Mathf.Exp(-14 * dt);
    if (_resetting)
    {
      _yaw = Mathf.Lerp(_yaw, InitialYaw, settle);
      _pitch = Mathf.Lerp(_pitch, InitialPitch, settle);
      _zoom = Mathf.Lerp(_zoom, 1, settle);
      return;
    }
    if (!held && (!_twisting || Time.unscaledTime - _lastDrag > .065f))
    {
      _velocity *= reduceMotion ? 0 : Mathf.Exp(-9 * dt);
      _yaw += _velocity.x * dt;
      _pitch += _velocity.y * dt;
      if (Time.unscaledTime - _lastDrag > .08f)
        _pitch = Mathf.Lerp(_pitch, Mathf.Clamp(_pitch, 15, 75), settle);
    }
    else if (Time.unscaledTime - _lastDrag > .08f)
      _velocity = Vector2.zero;
    if (Time.unscaledTime - _lastZoom > .065f)
    {
      _zoomVelocity *= reduceMotion ? 0 : Mathf.Exp(-12 * dt);
      _zoom += _zoomVelocity * dt;
      _zoom = Mathf.Lerp(_zoom, Mathf.Clamp(_zoom, .55f, 1.9f), settle);
    }
  }
}
