using System;
using UnityEngine;

namespace ElementKit;

/// <summary>One color of a gradient, at a position from 0 to 1.</summary>
[Serializable]
public sealed class PaintStop
{
  /// <summary>From 0 at the start to 1 at the end.</summary>
  public float Position;

  /// <summary>The color, opacity in alpha.</summary>
  public Color Color = Color.white;
}
