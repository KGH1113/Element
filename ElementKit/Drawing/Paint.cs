using System;
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
