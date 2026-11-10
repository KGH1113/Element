using System.Collections.Generic;

namespace ElementKit.Elements;

/// <summary>
/// Where your properties sit on the editing screen. The screen itself is
/// Element's: the dimmed background, your element in the middle at its game
/// size, Cancel and Done, the keypad and values along the bottom, the color
/// picker and undo. You only place properties in its spots.
/// </summary>
/// <remarks>
/// The bar above the element holds up to <see cref="MaximumBar"/> properties
/// and always ends with Grow from. A number <see cref="Below"/> or
/// <see cref="Beside"/> the element reads like a shape's W and H.
/// </remarks>
public sealed class EditorLayout
{
  /// <summary>The most properties the bar holds, so it fits a small screen.</summary>
  public const int MaximumBar = 6;

  private readonly List<Property> _bar = new();

  internal IReadOnlyList<Property> BarItems => _bar;
  internal NumberProperty BelowItem { get; private set; }
  internal NumberProperty BesideItem { get; private set; }

  /// <summary>Adds buttons to the bar above the element, left to right.</summary>
  /// <remarks>A color opens the color picker, a number the keypad, a choice its options; a toggle flips on click.</remarks>
  public void Bar(params Property[] properties)
  {
    if (properties == null)
      return;
    foreach (Property property in properties)
      if (property != null)
        _bar.Add(property);
  }

  /// <summary>Shows a number under the element, centered, like a shape's width.</summary>
  public void Below(NumberProperty property) => BelowItem = property;

  /// <summary>Shows a number to the right of the element, like a shape's height.</summary>
  public void Beside(NumberProperty property) => BesideItem = property;

  // Every property on the screen, once each, bar first.
  internal IEnumerable<Property> All()
  {
    foreach (Property property in _bar)
      yield return property;
    if (BelowItem != null)
      yield return BelowItem;
    if (BesideItem != null)
      yield return BesideItem;
  }

  // Drops properties Attach refused, so the screen never shows them.
  internal void Keep(ICollection<Property> accepted)
  {
    _bar.RemoveAll(property => !accepted.Contains(property));
    if (BelowItem != null && !accepted.Contains(BelowItem))
      BelowItem = null;
    if (BesideItem != null && !accepted.Contains(BesideItem))
      BesideItem = null;
  }
}
