using System;
using System.Collections.Generic;
using Element.Application.Layout;
using Element.Presentation.Overlay.Elements;
using Element.Presentation.Ui.Graphics;
using Element.Presentation.Ui.Widgets;
using ElementUI.Ugui;
using UnityEngine;
using UnityEngine.UI;

namespace Element.Presentation.OverlayEditor.Canvas.Layers;

/// <summary>
/// The pictures on layer cards: still copies of what the elements draw,
/// together as they stand on the screen, made small to fit. A key viewer,
/// drawn outside the game, shows as a pane with keys. Pictures are made when
/// a card shows and again after the layout changed, never every frame.
/// </summary>
internal sealed class LayerThumbnails
{
  private readonly CanvasSession _session;
  private readonly List<Picture> _pictures = new();
  private readonly Vector3[] _corners = new Vector3[4];

  public LayerThumbnails(CanvasSession session) => _session = session;

  /// <summary>Draws <paramref name="ids"/> into <paramref name="frame"/> until the returned object is disposed.</summary>
  public IDisposable Fill(IReadOnlyList<string> ids, RectTransform frame)
  {
    if (frame.GetComponent<RectMask2D>() == null)
      frame.gameObject.AddComponent<RectMask2D>();
    Picture picture = new(this, ids, frame);
    _pictures.Add(picture);
    picture.Draw();
    return picture;
  }

  /// <summary>Draws every shown picture again, after what the elements look like may have changed.</summary>
  public void RefreshAll()
  {
    foreach (Picture picture in _pictures)
      picture.Draw();
  }

  private void Forget(Picture picture) => _pictures.Remove(picture);

  private IOverlayLayoutTarget Target(string id)
  {
    foreach (CanvasFrame frame in _session.Frames)
      if (frame.Target.Id == id)
        return frame.Target;
    return null;
  }

  // Where a key viewer shows, in screen pixels from the bottom left.
  private static Rect ScreenOf(IOverlayLayoutTarget target)
  {
    Rect bounds = target.Bounds;
    bounds.position -= CanvasSession.OffsetOf(target);
    return new Rect(bounds.x, Screen.height - bounds.yMax, bounds.width, bounds.height);
  }

  private sealed class Picture : IDisposable
  {
    private readonly LayerThumbnails _owner;
    private readonly IReadOnlyList<string> _ids;
    private readonly RectTransform _frame;
    private readonly List<(RectTransform Copy, Rect Screen)> _copies = new();
    private readonly List<GameObject> _made = new();

    public Picture(LayerThumbnails owner, IReadOnlyList<string> ids, RectTransform frame)
    {
      _owner = owner;
      _ids = ids;
      _frame = frame;
    }

    public void Draw()
    {
      Clear();
      if (_frame == null)
        return;
      Rect union = default;
      bool any = false;
      // Back to front, so the copies stack as the elements do.
      foreach (string id in _ids)
      {
        IOverlayLayoutTarget target = _owner.Target(id);
        if (target == null || !CanvasSelection.Selectable(target))
          continue;
        RectTransform copy = null;
        Rect screen;
        if (target is OverlayElementTarget element)
          copy = element.Snapshot(_frame, out screen);
        else
        {
          screen = ScreenOf(target);
          copy = Pane(screen);
        }
        if (copy == null)
          continue;
        _made.Add(copy.gameObject);
        _copies.Add((copy, screen));
        union = any
          ? Rect.MinMaxRect(
            Mathf.Min(union.xMin, screen.xMin),
            Mathf.Min(union.yMin, screen.yMin),
            Mathf.Max(union.xMax, screen.xMax),
            Mathf.Max(union.yMax, screen.yMax)
          )
          : screen;
        any = true;
      }
      if (!any || union.width <= 0 || union.height <= 0)
        return;
      float frameScale = Mathf.Max(.0001f, _frame.lossyScale.x);
      Vector2 room = _frame.rect.size * frameScale;
      // Fits inside with a little air; a tiny element grows at most twice.
      float fit = Mathf.Min(2, Mathf.Min(room.x * .88f / union.width, room.y * .8f / union.height));
      foreach ((RectTransform copy, Rect screen) in _copies)
      {
        // Centered on the picture at the size it had, then made small as a whole.
        Vector2 size = copy.rect.size;
        copy.anchorMin = copy.anchorMax = copy.pivot = new Vector2(.5f, .5f);
        copy.sizeDelta = size;
        float pixelsPerUnit = size.x > 0 ? screen.width / size.x : 1;
        copy.localScale = Vector3.one * (pixelsPerUnit * fit / frameScale);
        copy.anchoredPosition = (screen.center - union.center) * (fit / frameScale);
      }
    }

    // A key viewer's stand-in: a glassy pane with the key viewer mark, as large as the viewer.
    private RectTransform Pane(Rect screen)
    {
      if (screen.width <= 0 || screen.height <= 0)
        return null;
      GameObject pane = new("Key viewer pane", typeof(RectTransform));
      RectTransform rect = (RectTransform)pane.transform;
      rect.SetParent(_frame, false);
      float unit = Mathf.Max(.0001f, _frame.lossyScale.x);
      rect.sizeDelta = screen.size / unit;
      RoundedRect fill = pane.AddComponent<RoundedRect>();
      fill.Radius = Mathf.Min(screen.width, screen.height) * .12f / unit;
      fill.color = new Color(1, 1, 1, .14f);
      fill.raycastTarget = false;
      GameObject mark = new("Key viewer mark", typeof(RectTransform));
      RectTransform markRect = (RectTransform)mark.transform;
      markRect.SetParent(rect, false);
      float side = Mathf.Min(screen.width, screen.height) * .6f / unit;
      markRect.sizeDelta = new Vector2(side, side);
      UiIcon icon = mark.AddComponent<UiIcon>();
      icon.Kind = UiIconKind.KeyViewer;
      icon.color = new Color(1, 1, 1, .8f);
      icon.raycastTarget = false;
      return rect;
    }

    private void Clear()
    {
      foreach (GameObject made in _made)
        if (made != null)
          UiFactory.Destroy(made);
      _made.Clear();
      _copies.Clear();
    }

    public void Dispose()
    {
      Clear();
      _owner.Forget(this);
    }
  }
}
