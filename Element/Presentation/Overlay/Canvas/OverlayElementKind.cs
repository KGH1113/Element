using Element.Domain.Overlay.Values;
using Element.Presentation.Overlay.Elements;
using Element.Presentation.Overlay.Fonts;
using UnityEngine;

namespace Element.Presentation.Overlay.Canvas;

/// <summary>
/// How the overlay draws one kind of element: which saved layouts it owns,
/// how it makes an element and what it does each frame. Every kind makes its
/// elements on the canvas's one layer, where the saved order stacks them.
/// <see cref="OverlayCanvasHost"/> knows kinds only through this.
/// </summary>
internal abstract class OverlayElementKind
{
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

  public virtual void SetFonts(OverlayFonts fonts) { }

  public abstract void Clear();
}
