using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using Element.Configuration;

namespace Element.Domain.Elements;

/// <summary>A color with straight alpha, each channel 0–1.</summary>
public readonly struct OverlayRgba
{
  public readonly float R,
    G,
    B,
    A;

  public OverlayRgba(float r, float g, float b, float a)
  {
    R = r;
    G = g;
    B = b;
    A = a;
  }

  public OverlayRgba WithAlpha(float a) => new(R, G, B, a);

  /// <summary>"#RRGGBB" or "RRGGBB"; white when it can't be read.</summary>
  public static OverlayRgba Parse(string hex, float alpha = 1)
  {
    if (!string.IsNullOrEmpty(hex))
    {
      string digits = hex[0] == '#' ? hex.Substring(1) : hex;
      if (
        digits.Length == 6
        && int.TryParse(digits, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int value)
      )
        return new OverlayRgba(((value >> 16) & 255) / 255f, ((value >> 8) & 255) / 255f, (value & 255) / 255f, alpha);
    }
    return new OverlayRgba(1, 1, 1, alpha);
  }

  public string ToHex()
  {
    static int Byte(float channel) => Math.Max(0, Math.Min(255, (int)Math.Round(channel * 255)));
    return "#" + Byte(R).ToString("X2") + Byte(G).ToString("X2") + Byte(B).ToString("X2");
  }
}

/// <summary>A stop as drawn: where it sits (0–1) and its color.</summary>
public readonly struct OverlayGradientSample
{
  public readonly float Position;
  public readonly OverlayRgba Color;

  public OverlayGradientSample(float position, OverlayRgba color)
  {
    Position = position;
    Color = color;
  }
}

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

/// <summary>Gradient geometry and colors, shared by drawing, the editors and tests.</summary>
internal static class OverlayGradientMath
{
  /// <summary>Puts the handles where a fresh gradient of <paramref name="kind"/> has them.</summary>
  public static void ResetHandles(OverlayGradient gradient, OverlayGradientKind kind)
  {
    gradient.Kind = kind;
    bool linear = kind == OverlayGradientKind.Linear;
    gradient.StartX = linear ? 0 : .5f;
    gradient.StartY = .5f;
    gradient.EndX = 1;
    gradient.EndY = .5f;
    gradient.WidthX = linear ? 0 : .5f;
    gradient.WidthY = 1;
  }

  /// <summary>
  /// The handles in box units (y down). A linear gradient's width handle is
  /// derived: square to the gradient and as long, as Figma draws it.
  /// </summary>
  public static void Handles(
    OverlayGradient gradient,
    float width,
    float height,
    out Vector2 start,
    out Vector2 end,
    out Vector2 across
  )
  {
    start = new Vector2(gradient.StartX * width, gradient.StartY * height);
    end = new Vector2(gradient.EndX * width, gradient.EndY * height);
    if (gradient.Kind == OverlayGradientKind.Linear)
    {
      Vector2 u = end - start;
      across = start + new Vector2(-u.Y, u.X);
    }
    else
      across = new Vector2(gradient.WidthX * width, gradient.WidthY * height);
  }

  public static void SetHandles(
    OverlayGradient gradient,
    float width,
    float height,
    Vector2 start,
    Vector2 end,
    Vector2 across
  )
  {
    width = Math.Max(1e-3f, width);
    height = Math.Max(1e-3f, height);
    gradient.StartX = start.X / width;
    gradient.StartY = start.Y / height;
    gradient.EndX = end.X / width;
    gradient.EndY = end.Y / height;
    gradient.WidthX = across.X / width;
    gradient.WidthY = across.Y / height;
  }

  public static OverlayGradientFrame Frame(OverlayGradient gradient, float width, float height)
  {
    Handles(gradient, width, height, out Vector2 start, out Vector2 end, out Vector2 across);
    return new OverlayGradientFrame(gradient.Kind, start, end, across);
  }

  /// <summary>
  /// The color at <paramref name="t"/> between stops sorted by position.
  /// Colors mix with their opacity, so a stop fading out doesn't bring in
  /// the dark of its hidden color.
  /// </summary>
  public static OverlayRgba Sample(IReadOnlyList<OverlayGradientSample> stops, float t)
  {
    int count = stops.Count;
    if (count == 0)
      return new OverlayRgba(1, 1, 1, 1);
    if (count == 1 || t <= stops[0].Position)
      return stops[0].Color;
    if (t >= stops[count - 1].Position)
      return stops[count - 1].Color;
    int j = 0;
    while (j < count - 2 && t > stops[j + 1].Position)
      j++;
    OverlayGradientSample from = stops[j],
      to = stops[j + 1];
    float span = to.Position - from.Position;
    float f = span <= 1e-6f ? 1 : (t - from.Position) / span;
    OverlayRgba a = from.Color,
      b = to.Color;
    float alpha = a.A + (b.A - a.A) * f;
    if (alpha <= 1e-4f)
      return new OverlayRgba(a.R + (b.R - a.R) * f, a.G + (b.G - a.G) * f, a.B + (b.B - a.B) * f, 0);
    return new OverlayRgba(
      (a.R * a.A + (b.R * b.A - a.R * a.A) * f) / alpha,
      (a.G * a.A + (b.G * b.A - a.G * a.A) * f) / alpha,
      (a.B * a.A + (b.B * b.A - a.B * a.A) * f) / alpha,
      alpha
    );
  }

  /// <summary>
  /// The direction from the start handle to the end handle, in degrees with
  /// 90 running left to right and 180 top to bottom, as in ElementNote.
  /// </summary>
  public static float Angle(OverlayGradient gradient, float width, float height)
  {
    Handles(gradient, width, height, out Vector2 start, out Vector2 end, out _);
    Vector2 u = end - start;
    double degrees = Math.Atan2(u.Y, u.X) * 180 / Math.PI + 90;
    return (float)(((degrees % 360) + 360) % 360);
  }

  /// <summary>
  /// Turns the gradient to <paramref name="degrees"/>, keeping its length. A
  /// linear gradient turns about its middle; the others about their center,
  /// with the width handle staying square to the end handle.
  /// </summary>
  public static void SetAngle(OverlayGradient gradient, float degrees, float width, float height)
  {
    Handles(gradient, width, height, out Vector2 start, out Vector2 end, out Vector2 across);
    Vector2 u = end - start;
    float length = u.Length();
    if (length < 1e-4f)
      length = Math.Max(width, height) * .5f;
    double theta = (degrees - 90) * Math.PI / 180;
    Vector2 direction = new((float)Math.Cos(theta), (float)Math.Sin(theta));
    if (gradient.Kind == OverlayGradientKind.Linear)
    {
      Vector2 middle = (start + end) * .5f;
      start = middle - direction * length * .5f;
      end = middle + direction * length * .5f;
    }
    else
    {
      Vector2 v = across - start;
      float reach = v.Length();
      float side = Math.Sign(u.X * v.Y - u.Y * v.X);
      if (side == 0)
        side = 1;
      end = start + direction * length;
      across = start + new Vector2(-direction.Y, direction.X) * reach * side;
    }
    SetHandles(gradient, width, height, start, end, across);
  }

  /// <summary>
  /// Sorts stops by position. Stops at the same place keep their order, so a
  /// hard edge between two of them never flips.
  /// </summary>
  public static void Sort(List<OverlayGradientSample> samples)
  {
    for (int i = 1; i < samples.Count; i++)
    {
      OverlayGradientSample sample = samples[i];
      int j = i - 1;
      while (j >= 0 && samples[j].Position > sample.Position)
      {
        samples[j + 1] = samples[j];
        j--;
      }
      samples[j + 1] = sample;
    }
  }
}
