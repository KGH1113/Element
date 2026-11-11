using Element.Configuration;
using Element.Domain.Overlay.Values;
using Element.Presentation.Overlay.Elements;
using UnityEngine;

namespace Element.Presentation.Overlay.Canvas;

/// <summary>
/// How the overlay draws one kind of element: which saved layouts it owns,
/// the layer it draws on, how it makes an element and what it does each
/// frame. <see cref="OverlayCanvasHost"/> knows kinds only through this.
/// </summary>
internal abstract class OverlayElementKind
{
  /// <summary>The layer's name in the canvas.</summary>
  public abstract string LayerName { get; }

  /// <summary>Where its layer sits: 0 is the bottom.</summary>
  public abstract int LayerDepth { get; }

  /// <summary>The layer its elements draw on, set by the canvas.</summary>
  public Transform Layer { get; set; }

  public abstract bool Owns(string templateId);

  /// <summary>False while it can't draw (texts without the overlay font).</summary>
  public virtual bool CanCreate => true;

  /// <summary>A new element on its layer, kept in its list.</summary>
  public abstract OverlayElementTarget Create();

  /// <summary>Drops <paramref name="element"/> from its list; false when it isn't one of this kind.</summary>
  public abstract bool Forget(OverlayElementTarget element);

  /// <summary>Once a frame: each element redraws what changed.</summary>
  public abstract void Tick(OverlayValueMask changes, bool settle);

  /// <summary>After every element was placed again.</summary>
  public virtual void Applied() { }

  public virtual void SetFonts(Shared.OverlayFonts fonts) { }

  public abstract void Clear();
}
