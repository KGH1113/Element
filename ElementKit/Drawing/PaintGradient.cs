using System;
using System.Collections.Generic;
using UnityEngine;

namespace ElementKit;

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
