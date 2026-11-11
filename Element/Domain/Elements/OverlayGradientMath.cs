using System;
using System.Collections.Generic;
using System.Numerics;
using Element.Configuration;

namespace Element.Domain.Elements;

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
