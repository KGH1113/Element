using System;
using System.Collections.Generic;
using Element.Configuration;

namespace Element.Domain.Elements;

/// <summary>
/// What the color bar and the gradient handles do to a color: one color is
/// solid, adding a second makes it a gradient, and removing down to one makes
/// it solid again. Edits read plain positions and opacities; formulas only
/// move stops as drawn.
/// </summary>
internal static class OverlayGradientEdits
{
  /// <summary>How close (0–1) a dragged stop comes before it snaps.</summary>
  public const float SnapDistance = .018f;

  /// <summary>
  /// The opacity a second color starts at when the color follows the
  /// highlight: there is one color, so the stops differ only in opacity.
  /// </summary>
  public const float HighlightFade = .4f;

  private static readonly float[] Marks = { 0, .25f, .5f, .75f, 1 };

  /// <summary>
  /// Adds a color at <paramref name="position"/> (0–1) and returns its index.
  /// A solid color becomes a gradient whose new color shows at once: a
  /// neighbor of the current color, or with the highlight a lighter opacity.
  /// Later colors start as the gradient's color at that point. -1 when the
  /// gradient is full.
  /// </summary>
  public static int AddStop(OverlayColorSettings color, float position, string highlightHex)
  {
    position = Clamp01(position);
    if (!color.IsGradient)
    {
      OverlayGradient gradient = new();
      OverlayGradientMath.ResetHandles(gradient, OverlayGradientKind.Linear);
      float alpha = Clamp01(color.Alpha);
      OverlayGradientStop first = new()
      {
        Position = 0,
        Hex = color.Hex,
        Alpha = alpha,
        AlphaFormula = color.AlphaFormula ?? string.Empty,
      };
      // A click right on the color puts the new one there and moves the old
      // one to the other end.
      if (position < .08f)
        first.Position = 1;
      OverlayGradientStop added = color.Highlight
        ? new() { Position = position, Hex = color.Hex, Alpha = alpha > .6f ? HighlightFade : 1 }
        : new() { Position = position, Hex = Companion(color.Hex, highlightHex), Alpha = alpha };
      gradient.Stops.Add(first);
      gradient.Stops.Add(added);
      color.Gradient = gradient;
      Sync(color);
      return 1;
    }
    List<OverlayGradientStop> stops = color.Gradient.Stops;
    if (stops.Count >= OverlayGradient.MaximumStops)
      return -1;
    OverlayRgba at = OverlayGradientMath.Sample(Samples(color.Gradient), position);
    stops.Add(
      new OverlayGradientStop
      {
        Position = position,
        Hex = color.Highlight ? stops[0].Hex : at.ToHex(),
        Alpha = (float)Math.Round(at.A, 3),
      }
    );
    Sync(color);
    return stops.Count - 1;
  }

  /// <summary>
  /// Removes a color and returns the index of the one to pick next, the
  /// nearest. With one color left the color is solid again; -1 when there was
  /// only one.
  /// </summary>
  public static int RemoveStop(OverlayColorSettings color, int index)
  {
    if (!color.IsGradient || index < 0 || index >= color.Gradient.Stops.Count)
      return -1;
    List<OverlayGradientStop> stops = color.Gradient.Stops;
    float position = stops[index].Position;
    stops.RemoveAt(index);
    if (stops.Count == 1)
    {
      OverlayGradientStop last = stops[0];
      if (!color.Highlight)
        color.Hex = last.Hex;
      color.Alpha = last.Alpha;
      color.AlphaFormula = last.AlphaFormula ?? string.Empty;
      color.Gradient = null;
      return 0;
    }
    int nearest = 0;
    for (int i = 1; i < stops.Count; i++)
      if (Math.Abs(stops[i].Position - position) < Math.Abs(stops[nearest].Position - position))
        nearest = i;
    Sync(color);
    return nearest;
  }

  /// <summary>Mirrors the colors along the gradient. Stops following a value stay with it.</summary>
  public static void Flip(OverlayColorSettings color)
  {
    if (!color.IsGradient)
      return;
    foreach (OverlayGradientStop stop in color.Gradient.Stops)
      if (string.IsNullOrEmpty(stop.PositionFormula))
        stop.Position = (float)Math.Round(1 - stop.Position, 4);
    Sync(color);
  }

  /// <summary>Turns the gradient a quarter turn clockwise.</summary>
  public static void Rotate(OverlayGradient gradient, float width, float height)
  {
    gradient.AngleFormula = string.Empty;
    float angle = (float)Math.Round(OverlayGradientMath.Angle(gradient, width, height));
    OverlayGradientMath.SetAngle(gradient, (angle + 90) % 360, width, height);
  }

  /// <summary>Another kind starts from that kind's own handles.</summary>
  public static void SetKind(OverlayGradient gradient, OverlayGradientKind kind)
  {
    if (gradient.Kind == kind)
      return;
    gradient.AngleFormula = string.Empty;
    OverlayGradientMath.ResetHandles(gradient, kind);
  }

  /// <summary>
  /// Following the highlight keeps each stop's opacity; when they are all the
  /// same the gradient would vanish, so they fade from the first to
  /// <see cref="HighlightFade"/>. A custom color starts every stop from the
  /// highlight color, so nothing jumps.
  /// </summary>
  public static void SetHighlight(OverlayColorSettings color, bool highlight, string highlightHex)
  {
    if (color.Highlight == highlight)
      return;
    color.Highlight = highlight;
    if (!highlight)
      color.Hex = highlightHex;
    if (!color.IsGradient)
      return;
    List<OverlayGradientStop> stops = color.Gradient.Stops;
    if (!highlight)
    {
      foreach (OverlayGradientStop stop in stops)
        stop.Hex = highlightHex;
    }
    else if (stops.TrueForAll(stop => string.IsNullOrEmpty(stop.AlphaFormula) && stop.Alpha == stops[0].Alpha))
    {
      List<int> order = Order(color.Gradient);
      float alpha = stops[0].Alpha;
      for (int rank = 0; rank < order.Count; rank++)
      {
        float fade = 1 + (HighlightFade - 1) * rank / (order.Count - 1);
        stops[order[rank]].Alpha = (float)Math.Round(alpha * fade, 2);
      }
    }
    Sync(color);
  }

  /// <summary>
  /// A neighbor of <paramref name="hex"/>: a little further round the color
  /// wheel and a little brighter. Grays have no hue to turn, so they meet the
  /// highlight color, or a lighter or darker gray.
  /// </summary>
  public static string Companion(string hex, string highlightHex)
  {
    OverlayRgba color = OverlayRgba.Parse(hex);
    ToHsv(color, out float hue, out float saturation, out float value);
    if (saturation < .12f)
    {
      ToHsv(OverlayRgba.Parse(highlightHex), out _, out float highlightSaturation, out _);
      if (highlightSaturation >= .12f)
        return OverlayRgba.Parse(highlightHex).ToHex();
      return FromHsv(0, 0, value > .5f ? value - .45f : value + .45f).ToHex();
    }
    return FromHsv(
        (hue + 24) % 360,
        Math.Max(.2f, Math.Min(1, saturation * .9f)),
        value < .8f ? Math.Min(1, value + .2f) : value
      )
      .ToHex();
  }

  /// <summary>
  /// A dragged stop's position, pulled onto 0, 25, 50, 75 or 100% or another
  /// stop when it comes within <see cref="SnapDistance"/>.
  /// </summary>
  public static float Snap(OverlayGradient gradient, int index, float position)
  {
    float best = position,
      distance = SnapDistance;
    void Try(float mark)
    {
      float d = Math.Abs(mark - position);
      if (d < distance)
      {
        distance = d;
        best = mark;
      }
    }
    foreach (float mark in Marks)
      Try(mark);
    if (gradient?.Stops != null)
      for (int i = 0; i < gradient.Stops.Count; i++)
        if (i != index)
          Try(gradient.Stops[i].Position);
    return best;
  }

  /// <summary>Stop indexes from the start of the gradient to its end.</summary>
  public static List<int> Order(OverlayGradient gradient)
  {
    List<int> order = new();
    if (gradient?.Stops == null)
      return order;
    for (int i = 0; i < gradient.Stops.Count; i++)
    {
      int at = order.Count;
      while (at > 0 && gradient.Stops[order[at - 1]].Position > gradient.Stops[i].Position)
        at--;
      order.Insert(at, i);
    }
    return order;
  }

  /// <summary>The first color keeps the solid fields, which earlier versions draw.</summary>
  public static void Sync(OverlayColorSettings color)
  {
    if (!color.IsGradient)
      return;
    OverlayGradientStop first = color.Gradient.Stops[Order(color.Gradient)[0]];
    if (!color.Highlight)
      color.Hex = first.Hex;
    color.Alpha = first.Alpha;
    color.AlphaFormula = first.AlphaFormula ?? string.Empty;
  }

  /// <summary>The stops with their plain positions and opacities, sorted.</summary>
  public static List<OverlayGradientSample> Samples(OverlayGradient gradient, string highlightHex = null)
  {
    List<OverlayGradientSample> samples = new();
    foreach (OverlayGradientStop stop in gradient.Stops)
      samples.Add(
        new OverlayGradientSample(stop.Position, OverlayRgba.Parse(highlightHex ?? stop.Hex, Clamp01(stop.Alpha)))
      );
    OverlayGradientMath.Sort(samples);
    return samples;
  }

  private static float Clamp01(float value) => value < 0 ? 0 : value > 1 ? 1 : value;

  private static void ToHsv(OverlayRgba color, out float hue, out float saturation, out float value)
  {
    float max = Math.Max(color.R, Math.Max(color.G, color.B)),
      min = Math.Min(color.R, Math.Min(color.G, color.B)),
      delta = max - min;
    hue = 0;
    if (delta > 0)
    {
      if (max == color.R)
        hue = 60 * (((color.G - color.B) / delta) % 6);
      else if (max == color.G)
        hue = 60 * ((color.B - color.R) / delta + 2);
      else
        hue = 60 * ((color.R - color.G) / delta + 4);
      if (hue < 0)
        hue += 360;
    }
    saturation = max > 0 ? delta / max : 0;
    value = max;
  }

  private static OverlayRgba FromHsv(float hue, float saturation, float value)
  {
    float c = value * saturation,
      x = c * (1 - Math.Abs(hue / 60 % 2 - 1)),
      m = value - c;
    float r,
      g,
      b;
    if (hue < 60)
      (r, g, b) = (c, x, 0);
    else if (hue < 120)
      (r, g, b) = (x, c, 0);
    else if (hue < 180)
      (r, g, b) = (0, c, x);
    else if (hue < 240)
      (r, g, b) = (0, x, c);
    else if (hue < 300)
      (r, g, b) = (x, 0, c);
    else
      (r, g, b) = (c, 0, x);
    return new OverlayRgba(r + m, g + m, b + m, 1);
  }
}
