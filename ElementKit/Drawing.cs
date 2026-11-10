using TMPro;
using UnityEngine;

namespace ElementKit;

/// <summary>
/// Helpers for drawing an element the way Element's own look: words in the
/// overlay's font (the one players pick as the default), boxes with soft
/// rounded corners, and the highlight color. Draw anything else with UGUI.
/// </summary>
public abstract class Drawing
{
  /// <summary>The highlight color players picked; it can change while the element is shown.</summary>
  public abstract Color Highlight { get; }

  /// <summary>
  /// A text in the overlay's font under <paramref name="parent"/>, top left at
  /// (0, 0). It follows the font when players pick another.
  /// </summary>
  public abstract TextMeshProUGUI Text(Transform parent, string text = "", float size = 24);

  /// <summary>A filled box with rounded corners under <paramref name="parent"/>, edges kept smooth at any size.</summary>
  public abstract RoundedBox Box(Transform parent);

  /// <summary>
  /// Puts <paramref name="rect"/>'s top left at (<paramref name="x"/>, <paramref name="y"/>)
  /// in its parent, y down, <paramref name="width"/> by <paramref name="height"/>.
  /// </summary>
  public static void Place(RectTransform rect, float x, float y, float width, float height)
  {
    if (rect == null)
      return;
    Vector2 topLeft = new(0, 1);
    if (rect.anchorMin != topLeft)
      rect.anchorMin = topLeft;
    if (rect.anchorMax != topLeft)
      rect.anchorMax = topLeft;
    if (rect.pivot != topLeft)
      rect.pivot = topLeft;
    Vector2 position = new(x, -y),
      size = new(width, height);
    if (rect.anchoredPosition != position)
      rect.anchoredPosition = position;
    if (rect.sizeDelta != size)
      rect.sizeDelta = size;
  }
}

/// <summary>A filled box with rounded corners.</summary>
public abstract class RoundedBox
{
  /// <summary>The box's UI object; place it with <see cref="Drawing.Place"/>.</summary>
  public abstract RectTransform Rect { get; }

  /// <summary>The fill, opacity included.</summary>
  public abstract Color Color { get; set; }

  /// <summary>The corners' radius in the box's units; kept within half its shorter side.</summary>
  public abstract float Rounding { get; set; }

  /// <summary>Whether the box shows.</summary>
  public abstract bool Visible { get; set; }
}
