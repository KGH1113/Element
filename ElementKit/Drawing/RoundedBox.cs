using UnityEngine;

namespace ElementKit;

/// <summary>A filled box with rounded corners.</summary>
public abstract class RoundedBox
{
  // Only Element implements this: a mod uses the one Element hands it and
  // cannot subclass it, so new members never break mods.
  internal RoundedBox() { }

  /// <summary>The box's UI object; place it with <see cref="Drawing.Place"/>.</summary>
  public abstract RectTransform Rect { get; }

  /// <summary>The fill as one color, opacity included.</summary>
  public abstract Color Color { get; set; }

  /// <summary>
  /// The fill as players picked it: the highlight color (following it when it
  /// changes), a color of their own, or a gradient across the box. Setting it
  /// replaces <see cref="Color"/>.
  /// </summary>
  public abstract Paint Paint { get; set; }

  /// <summary>The corners' radius in the box's units; kept within half its shorter side.</summary>
  public abstract float Rounding { get; set; }

  /// <summary>Whether the box shows.</summary>
  public abstract bool Visible { get; set; }
}
