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
  // Only Element implements this: a mod uses the one Element hands it and
  // cannot subclass it, so new members never break mods.
  internal Drawing() { }

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
  /// Draws <paramref name="text"/> in <paramref name="font"/>, a font
  /// <see cref="EditorPage.FontPicker"/> gave; empty for the overlay's default.
  /// A font still loading or missing on this PC shows as the default until it is there.
  /// </summary>
  public abstract void SetFont(TextMeshProUGUI text, string font);

  /// <summary><paramref name="paint"/> as one color for your own drawing: the highlight worked out, a gradient's first color.</summary>
  public Color Solid(Paint paint) => paint == null ? Color.white : paint.ToColor(Highlight);

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
