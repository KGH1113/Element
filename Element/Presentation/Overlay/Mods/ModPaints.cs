using System.Collections.Generic;
using Element.Configuration;
using Element.Presentation.Overlay.Elements;
using ElementKit;
using UnityEngine;

namespace Element.Presentation.Overlay.Mods;

/// <summary>
/// A mod's <see cref="Paint"/> as Element's own color settings and back, so
/// the color picker edits it and Element's shapes draw it, gradient included.
/// </summary>
internal static class ModPaints
{
  public static OverlayColorSettings ToSettings(Paint paint)
  {
    paint ??= Paint.Solid(Color.white);
    OverlayColorSettings color = paint.Highlight
      ? OverlayColorSettings.HighlightColor(paint.Color.a)
      : OverlayColorSettings.Custom(Hex(paint.Color), paint.Color.a);
    if (!paint.IsGradient)
      return color;
    PaintGradient source = paint.Gradient;
    OverlayGradient gradient = new()
    {
      Kind = (OverlayGradientKind)(int)source.Kind,
      StartX = source.Start.x,
      StartY = source.Start.y,
      EndX = source.End.x,
      EndY = source.End.y,
      WidthX = source.Across.x,
      WidthY = source.Across.y,
    };
    foreach (PaintStop stop in source.Stops)
      if (stop != null && gradient.Stops.Count < OverlayGradient.MaximumStops)
        gradient.Stops.Add(
          new OverlayGradientStop
          {
            Position = Mathf.Clamp01(stop.Position),
            Hex = Hex(stop.Color),
            Alpha = Mathf.Clamp01(stop.Color.a),
          }
        );
    color.Gradient = gradient;
    if (color.IsGradient)
    {
      // Hex and Alpha keep the first color, as Element's own colors do.
      color.Hex = gradient.Stops[0].Hex;
      color.Alpha = gradient.Stops[0].Alpha;
    }
    return color;
  }

  public static Paint FromSettings(OverlayColorSettings color)
  {
    if (color == null)
      return Paint.Solid(Color.white);
    Color solid = OverlayTextComposer.ParseHex(color.Hex);
    solid.a = Mathf.Clamp01(color.Alpha);
    Paint paint = new() { Highlight = color.Highlight, Color = solid };
    if (!color.IsGradient)
      return paint;
    OverlayGradient source = color.Gradient;
    paint.Gradient = new PaintGradient
    {
      Kind = (PaintKind)(int)source.Kind,
      Start = new Vector2(source.StartX, source.StartY),
      End = new Vector2(source.EndX, source.EndY),
      Across = new Vector2(source.WidthX, source.WidthY),
      Stops = new List<PaintStop>(),
    };
    foreach (OverlayGradientStop stop in source.Stops)
    {
      Color stopColor = OverlayTextComposer.ParseHex(stop.Hex);
      stopColor.a = Mathf.Clamp01(stop.Alpha);
      paint.Gradient.Stops.Add(new PaintStop { Position = Mathf.Clamp01(stop.Position), Color = stopColor });
    }
    return paint;
  }

  private static string Hex(Color color) => "#" + ColorUtility.ToHtmlStringRGB(color);
}
