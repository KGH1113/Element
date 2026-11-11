using System;
using System.Collections.Generic;
using UnityEngine;

namespace ElementKit;

/// <summary>
/// A color the way Element's own elements have it: one color, the player's
/// highlight color, or a gradient. Keep it in your settings class; pick it
/// with <see cref="EditorPage.ColorPicker(string, Paint, Action{Paint})"/> and
/// fill a box with <see cref="RoundedBox.Paint"/>.
/// </summary>
[Serializable]
public sealed class Paint
{
  /// <summary>Follows the highlight color players picked; only its opacity is the paint's own.</summary>
  public bool Highlight;

  /// <summary>The color, opacity in alpha. With a gradient, its first color.</summary>
  public Color Color = Color.white;

  /// <summary>Two or more colors across the box; null or fewer than two for one color.</summary>
  public PaintGradient Gradient;

  /// <summary>One color.</summary>
  public static Paint Solid(Color color) => new() { Color = color };

  /// <summary>The player's highlight color at <paramref name="opacity"/>.</summary>
  public static Paint HighlightColor(float opacity = 1) =>
    new() { Highlight = true, Color = new Color(1, 1, 1, opacity) };

  /// <summary>Whether it is a gradient.</summary>
  public bool IsGradient => Gradient != null && Gradient.Stops != null && Gradient.Stops.Count > 1;

  /// <summary>The paint as one color: the highlight worked out, a gradient's first color.</summary>
  public Color ToColor(Color highlight)
  {
    Color color = Highlight ? highlight : Color;
    color.a = Color.a;
    return color;
  }

  /// <summary>A copy that shares nothing with this one.</summary>
  public Paint Clone() =>
    new()
    {
      Highlight = Highlight,
      Color = Color,
      Gradient = Gradient?.Clone(),
    };
}

/// <summary>How a gradient's colors spread over the box.</summary>
public enum PaintKind
{
  /// <summary>Along a line.</summary>
  Linear,

  /// <summary>Outward from the start point.</summary>
  Radial,

  /// <summary>Around the start point.</summary>
  Angular,

  /// <summary>In diamonds around the start point.</summary>
  Diamond,
}

/// <summary>A gradient's colors and where it runs, as fractions of the box (y down).</summary>
[Serializable]
public sealed class PaintGradient
{
  /// <summary>How the colors spread.</summary>
  public PaintKind Kind;

  /// <summary>The colors, at most eight.</summary>
  public List<PaintStop> Stops = new();

  /// <summary>Where it starts: the center of radial, angular and diamond gradients.</summary>
  public Vector2 Start = new(0, .5f);

  /// <summary>Where it ends.</summary>
  public Vector2 End = new(1, .5f);

  /// <summary>How far radial, angular and diamond gradients reach across.</summary>
  public Vector2 Across = new(0, 1);

  /// <summary>A copy that shares nothing with this one.</summary>
  public PaintGradient Clone()
  {
    PaintGradient copy = new()
    {
      Kind = Kind,
      Start = Start,
      End = End,
      Across = Across,
    };
    if (Stops != null)
      foreach (PaintStop stop in Stops)
        copy.Stops.Add(new PaintStop { Position = stop.Position, Color = stop.Color });
    return copy;
  }
}

/// <summary>One color of a gradient, at a position from 0 to 1.</summary>
[Serializable]
public sealed class PaintStop
{
  /// <summary>From 0 at the start to 1 at the end.</summary>
  public float Position;

  /// <summary>The color, opacity in alpha.</summary>
  public Color Color = Color.white;
}
