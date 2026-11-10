using UnityEngine;

namespace ElementKit.Elements;

/// <summary>Where a text sits across its box.</summary>
public enum TextAlign
{
  /// <summary>Against the left edge.</summary>
  Left,

  /// <summary>In the middle.</summary>
  Center,

  /// <summary>Against the right edge.</summary>
  Right,
}

/// <summary>
/// A color as players picked it. Pass it to <see cref="Surface.Box"/> or
/// <see cref="Surface.Text"/> and Element draws it the way its own elements do,
/// gradient and highlight color included.
/// </summary>
public readonly struct ElementColor
{
  // How much of the picked opacity is taken away: zero by default, so a
  // color made any way starts unfaded. Kept as a number, so fading allocates nothing.
  private readonly float _dim;

  internal ElementColor(Color solid, object paint, float dim = 0)
  {
    Solid = solid;
    Paint = paint;
    _dim = dim;
  }

  /// <summary>
  /// The color as one color, for your own drawing: the highlight color worked
  /// out, a gradient's first color, the opacity in alpha.
  /// </summary>
  public Color Solid { get; }

  // Element's own description of the color, gradient included; null for From.
  internal object Paint { get; }

  // The share of the picked opacity Element draws Paint with.
  internal float Opacity => 1 - _dim;

  /// <summary>A color of your own, drawn as it is.</summary>
  public static ElementColor From(Color color) => new(color, null);

  /// <summary>The same color with its opacity multiplied by <paramref name="factor"/>. Allocates nothing.</summary>
  public ElementColor Faded(float factor)
  {
    factor = Mathf.Clamp01(factor);
    Color solid = Solid;
    solid.a *= factor;
    return new ElementColor(solid, Paint, 1 - Opacity * factor);
  }
}

/// <summary>
/// Your element's properties as the player set them, with values worked out.
/// Read one with its property: <c>props[Width]</c>.
/// </summary>
public abstract class Props
{
  /// <summary>The number, worked out from its values, kept between the property's limits.</summary>
  public abstract double this[NumberProperty property] { get; }

  /// <summary>The color.</summary>
  public abstract ElementColor this[ColorProperty property] { get; }

  /// <summary>The index of the picked option in <see cref="ChoiceProperty.Options"/>.</summary>
  public abstract int this[ChoiceProperty property] { get; }

  /// <summary>Whether it is on.</summary>
  public abstract bool this[ToggleProperty property] { get; }

  /// <summary>
  /// True in Element's editors, where values show their samples. Draw sample
  /// data when your mod has nothing to show yet, so players see what they edit.
  /// </summary>
  public abstract bool IsPreview { get; }
}

/// <summary>
/// What your element draws into. Its top left is (0, 0), y grows down, and a
/// unit is a pixel at 1080p. Call the drawing methods in the same order on
/// every draw: Element keeps what you drew last time and changes only what differs.
/// </summary>
public abstract class Surface
{
  /// <summary>The element's size, as <see cref="ElementType.Measure"/> gave it.</summary>
  public abstract Vector2 Size { get; }

  /// <summary>
  /// A UI object for drawing of your own (UGUI), above what the methods here draw.
  /// It is the element's size, pivot at the top left. Element never touches its children.
  /// </summary>
  public abstract RectTransform Root { get; }

  /// <summary>Anything you want to keep with this one element, such as objects you made under <see cref="Root"/>.</summary>
  public object State { get; set; }

  /// <summary>A filled rectangle with rounded corners, in <paramref name="fill"/>'s color or gradient.</summary>
  public abstract void Box(Rect rect, ElementColor fill, float rounding = 0);

  /// <summary>One line of text in the overlay's font, centered vertically in <paramref name="rect"/>.</summary>
  public abstract void Text(Rect rect, string text, float size, ElementColor color, TextAlign align = TextAlign.Center);
}
