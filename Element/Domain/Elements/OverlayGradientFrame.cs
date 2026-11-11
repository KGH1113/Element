using System;
using System.Numerics;
using Element.Configuration;

namespace Element.Domain.Elements;

/// <summary>
/// Where each point of a box falls along a gradient. For a point P, solving
/// P - start = a·(end - start) + b·(width - start) gives (a, b); then linear
/// uses a, radial the distance √(a² + b²), diamond |a| + |b|, and angular the
/// angle of (a, b) as a turn, clockwise on screen from the end handle.
/// </summary>
public readonly struct OverlayGradientFrame
{
  private readonly OverlayGradientKind _kind;
  private readonly float _ox,
    _oy,
    _ax,
    _ay,
    _bx,
    _by;

  public OverlayGradientFrame(OverlayGradientKind kind, Vector2 start, Vector2 end, Vector2 width)
  {
    _kind = kind;
    _ox = start.X;
    _oy = start.Y;
    Vector2 u = end - start,
      v = width - start;
    float determinant = u.X * v.Y - u.Y * v.X;
    // Handles on one line fall back to a width handle square to the gradient.
    if (Math.Abs(determinant) < 1e-6f)
    {
      v = new Vector2(-u.Y, u.X);
      determinant = u.X * v.Y - u.Y * v.X;
    }
    Valid = Math.Abs(determinant) >= 1e-6f;
    if (!Valid)
      determinant = 1;
    _ax = v.Y / determinant;
    _ay = -v.X / determinant;
    _bx = -u.Y / determinant;
    _by = u.X / determinant;
  }

  /// <summary>False when the start and end handles meet; the gradient shows its first color.</summary>
  public bool Valid { get; }

  /// <summary>Where (x, y) in box units falls along the gradient; not clamped.</summary>
  public float At(float x, float y)
  {
    if (!Valid)
      return 0;
    float px = x - _ox,
      py = y - _oy;
    float a = px * _ax + py * _ay,
      b = px * _bx + py * _by;
    switch (_kind)
    {
      case OverlayGradientKind.Radial:
        return (float)Math.Sqrt(a * a + b * b);
      case OverlayGradientKind.Diamond:
        return Math.Abs(a) + Math.Abs(b);
      case OverlayGradientKind.Angular:
        float turn = (float)(Math.Atan2(b, a) / (Math.PI * 2));
        return turn < 0 ? turn + 1 : turn;
      default:
        return a;
    }
  }
}
