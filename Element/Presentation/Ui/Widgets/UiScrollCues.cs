using System;
using Element.Presentation.Ui.Theme;
using UnityEngine;
using UnityEngine.UI;

namespace Element.Presentation.Ui.Widgets;

/// <summary>
/// Says a scroll view holds more than shows, the way a list on a phone
/// does: a slim bar along its right edge, as long as the share that shows
/// and where it is, and its edges fading into the color under it where more
/// lies beyond (the bottom while more is below, the top once scrolled down).
/// Each fade comes in over a little scrolling, so it is gone at the very
/// end. All of it lies over the content, clipped with it, and takes no
/// clicks; none of it shows while everything fits. For a scroll view on an
/// opaque base of that color, whose corners the fades round like the base's.
/// </summary>
/// <remarks>
/// <c>.Configure&lt;ScrollRect&gt;(scroll =&gt; UiScrollCues.Add(scroll, color, radius))</c>;
/// disposed with the view. It follows the scroll view's own change event,
/// which fires on scrolling and when the content or the frame changes size,
/// so it costs nothing while nothing moves.
/// </remarks>
internal sealed class UiScrollCues : IDisposable
{
  /// <summary>How tall the fade at each edge is.</summary>
  public const float FadeHeight = 28;

  /// <summary>The scrolling over which a fade comes in, so it is gone at the very end.</summary>
  public const float Reach = 16;

  private const float BarWidth = 3,
    BarInset = 3,
    MinimumBar = 24;

  private static readonly Color BarInk = new(UiPalette.Muted.r, UiPalette.Muted.g, UiPalette.Muted.b, .42f);

  private readonly ScrollRect _scroll;
  private readonly RectTransform _viewport;
  private readonly UiEdgeFadeGraphic _top;
  private readonly UiEdgeFadeGraphic _bottom;
  private readonly UiCapsuleGraphic _bar;
  private readonly System.Collections.Generic.List<UiSizeWatcher> _watched = new();

  private UiScrollCues(ScrollRect scroll, Color under, float radius)
  {
    _scroll = scroll;
    _viewport = scroll.viewport != null ? scroll.viewport : (RectTransform)scroll.transform;
    _top = UiEdgeFadeGraphic.Make(_viewport, "Top fade", under, true, FadeHeight, radius);
    _bottom = UiEdgeFadeGraphic.Make(_viewport, "Bottom fade", under, false, FadeHeight, radius);
    _bar = MakeBar(_viewport);
    scroll.onValueChanged.AddListener(Changed);
    // What shows may grow or shrink without scrolling: the bar and the fades follow that too.
    Watch(scroll.content);
    Watch(_viewport);
    Follow();
  }

  private void Watch(RectTransform rect)
  {
    if (rect == null)
      return;
    UiSizeWatcher watcher = rect.GetComponent<UiSizeWatcher>();
    if (watcher == null)
      watcher = rect.gameObject.AddComponent<UiSizeWatcher>();
    watcher.Changed += Follow;
    _watched.Add(watcher);
  }

  /// <summary>Cues on <paramref name="scroll"/>, its fades in <paramref name="under"/> (the color of the base under it) rounded by its <paramref name="radius"/>.</summary>
  public static UiScrollCues Add(ScrollRect scroll, Color under, float radius) => new(scroll, under, radius);

  public void Dispose()
  {
    if (_scroll != null)
      _scroll.onValueChanged.RemoveListener(Changed);
    foreach (UiSizeWatcher watcher in _watched)
      if (watcher != null)
        watcher.Changed -= Follow;
    _watched.Clear();
  }

  private void Changed(Vector2 _) => Follow();

  // How much lies beyond each edge, shown by the fades and the bar.
  private void Follow()
  {
    if (_scroll == null || _scroll.content == null)
      return;
    float height = _viewport.rect.height;
    float overflow = _scroll.content.rect.height - height;
    bool more = overflow > .5f;
    // The content's top moves up as it scrolls down.
    float scrolled = more ? Mathf.Clamp(_scroll.content.anchoredPosition.y, 0, overflow) : 0;
    _top.Show(more ? scrolled / Reach : 0);
    _bottom.Show(more ? (overflow - scrolled) / Reach : 0);
    if (_bar.enabled != more)
      _bar.enabled = more;
    if (more)
      PlaceBar(height, overflow, scrolled);
  }

  // As long as the share that shows, down the track as far as it is scrolled; in half units, so scrolling moves it only when it shows.
  private void PlaceBar(float height, float overflow, float scrolled)
  {
    float track = height - BarInset * 2;
    float length = Mathf.Round(Mathf.Clamp(track * height / (height + overflow), MinimumBar, track) * 2) / 2;
    float top = Mathf.Round((BarInset + (track - length) * scrolled / overflow) * 2) / 2;
    RectTransform bar = _bar.rectTransform;
    Vector2 size = new(BarWidth, length),
      at = new(-BarInset, -top);
    if (bar.sizeDelta != size)
      bar.sizeDelta = size;
    if (bar.anchoredPosition != at)
      bar.anchoredPosition = at;
  }

  // The bar, along the right edge over the fades.
  private static UiCapsuleGraphic MakeBar(RectTransform viewport)
  {
    GameObject go = new("Scroll bar", typeof(RectTransform), typeof(CanvasRenderer));
    go.layer = viewport.gameObject.layer;
    RectTransform rect = (RectTransform)go.transform;
    rect.SetParent(viewport, false);
    rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1, 1);
    rect.SetAsLastSibling();
    UiCapsuleGraphic bar = go.AddComponent<UiCapsuleGraphic>();
    bar.color = BarInk;
    bar.raycastTarget = false;
    bar.enabled = false;
    return bar;
  }
}
