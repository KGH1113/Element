using Element.Presentation.Ui.Widgets;
using UnityEngine;

namespace Element.Presentation.Ui.Popovers;

/// <summary>
/// Where a popover opens next to what opened it, in the units of the screen
/// it opens on (origin top left), kept <see cref="Margin"/> inside the view.
/// </summary>
internal static class PopoverPlacement
{
  public const float Margin = 8;

  /// <summary>A popover at <paramref name="x"/>, <paramref name="y"/>, moved inside the view.</summary>
  public static Rect Clamp(float x, float y, float width, float height, float viewWidth, float viewHeight) =>
    new(
      Mathf.Clamp(x, Margin, Mathf.Max(Margin, viewWidth - width - Margin)),
      Mathf.Clamp(y, Margin, Mathf.Max(Margin, viewHeight - height - Margin)),
      width,
      height
    );

  /// <summary>Under <paramref name="anchor"/>, or above it when the view ends below.</summary>
  public static Rect Under(
    Rect anchor,
    float x,
    float width,
    float height,
    float gap,
    float viewWidth,
    float viewHeight
  )
  {
    float y = anchor.yMax + gap;
    if (y + height > viewHeight - Margin)
      y = anchor.y - gap - height;
    return Clamp(x, y, width, height, viewWidth, viewHeight);
  }

  /// <summary>Above <paramref name="anchor"/>, or under it when there is no room above.</summary>
  public static Rect Above(
    Rect anchor,
    float x,
    float width,
    float height,
    float gap,
    float viewWidth,
    float viewHeight
  )
  {
    float y = anchor.y - height - gap;
    if (y < Margin)
      y = anchor.yMax + gap;
    return Clamp(x, y, width, height, viewWidth, viewHeight);
  }

  /// <summary>Right of <paramref name="anchor"/>, or left of it when the view ends on the right.</summary>
  public static Rect Beside(
    Rect anchor,
    float y,
    float width,
    float height,
    float gap,
    float viewWidth,
    float viewHeight
  )
  {
    float x = anchor.xMax + gap;
    if (x + width > viewWidth - Margin)
      x = anchor.x - width - gap;
    return Clamp(x, y, width, height, viewWidth, viewHeight);
  }

  /// <summary>Places <paramref name="popover"/> at <paramref name="bounds"/>.</summary>
  public static void Place(RectTransform popover, Rect bounds) =>
    UiFactory.Place(popover, bounds.x, bounds.y, bounds.width, bounds.height);

  /// <summary>
  /// Places a popover laid out at <paramref name="width"/> × <paramref name="height"/>
  /// into <paramref name="bounds"/>, scaled to its width.
  /// </summary>
  public static void PlaceScaled(RectTransform popover, Rect bounds, float width, float height)
  {
    UiFactory.Place(popover, bounds.x, bounds.y, width, height);
    popover.localScale = Vector3.one * (bounds.width / width);
  }

  /// <summary>Where a popover on a screen-sized canvas is, in screen pixels from the top left.</summary>
  public static Rect ScreenRect(RectTransform rect)
  {
    Vector2 position = rect.anchoredPosition;
    Vector2 size = rect.rect.size * rect.localScale.x;
    return new Rect(position.x, -position.y, size.x, size.y);
  }

  /// <summary>Whether a screen point (origin bottom left) is over <paramref name="rect"/>.</summary>
  public static bool Contains(RectTransform rect, Vector2 screen) =>
    rect != null
    && rect.gameObject.activeInHierarchy
    && RectTransformUtility.RectangleContainsScreenPoint(rect, screen, null);
}
