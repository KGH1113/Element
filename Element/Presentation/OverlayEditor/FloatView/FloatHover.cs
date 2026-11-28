using System;
using Element.Application.Layout;
using Element.Presentation.OverlayEditor.Canvas;
using Element.Presentation.Ui.Theme;
using TMPro;
using UnityEngine;

namespace Element.Presentation.OverlayEditor.FloatView;

/// <summary>A single screen-space outline and upright name tag, shared by all faces.</summary>
internal sealed class FloatHover
{
  private readonly CanvasSession _session;
  private readonly Func<IOverlayLayoutTarget, string> _label;
  private readonly Func<Rect> _toolbar;
  private readonly RectTransform _root,
    _tag;
  private readonly RectTransform[] _edges = new RectTransform[4];
  private readonly TextMeshProUGUI _caption;
  public FloatLayer Target { get; private set; }

  public FloatHover(
    CanvasSession session,
    RectTransform parent,
    Func<IOverlayLayoutTarget, string> label,
    Func<Rect> toolbar
  )
  {
    _session = session;
    _label = label;
    _toolbar = toolbar;
    _root = session.Ui.Rect("Float hover", parent, 0, 0, session.ViewWidth, session.ViewHeight);
    for (int i = 0; i < _edges.Length; i++)
    {
      var edge = session.Ui.Panel("Outline edge", _root, 0, 0, 0, 0, UiPalette.Accent, false).rectTransform;
      edge.pivot = new Vector2(.5f, .5f);
      _edges[i] = edge;
    }
    _tag = session.Ui.Panel("Element name", _root, 0, 0, 0, 0, UiPalette.Action).rectTransform;
    _caption = session.Ui.Text("", _tag, 0, 0, 0, 0, 13, UiPalette.OnAction);
    _caption.alignment = TextAlignmentOptions.Center;
    _caption.textWrappingMode = TextWrappingModes.NoWrap;
    _root.gameObject.SetActive(false);
  }

  public void Show(FloatLayer next)
  {
    if (Target == next)
      return;
    if (Target != null)
      Target.Visual.anchoredPosition = Vector2.zero;
    Target = next;
    _root.gameObject.SetActive(next != null);
    if (next == null)
      return;
    float unit = _session.UiScale;
    _caption.text = _label(next.Target);
    _caption.fontSize = 13 * unit;
    float width = Mathf.Min(Mathf.Ceil(_caption.preferredWidth) + 16 * unit, _session.ViewWidth - 12 * unit);
    _tag.sizeDelta = new Vector2(width, 23 * unit);
    _caption.rectTransform.sizeDelta = _tag.sizeDelta;
    Place();
  }

  public void Place()
  {
    if (Target == null)
      return;
    float unit = _session.UiScale;
    float lift = _session.Preferences.ReduceUiMotion ? 0 : 3 * unit;
    // Move only the artwork. The pointer can stay on the edge without changing its own hit test.
    Target.Visual.anchoredPosition = Target.Face.InverseTransformVector(_root.TransformVector(Vector3.up * lift));
    Vector2[] corners = Target.Footprint.Corners;
    for (int i = 0; i < corners.Length; i++)
    {
      Vector2 a = corners[i],
        b = corners[(i + 1) % corners.Length];
      Vector2 direction = new(b.x - a.x, a.y - b.y);
      RectTransform edge = _edges[i];
      edge.anchoredPosition = new Vector2((a.x + b.x) * .5f, -(a.y + b.y) * .5f + lift);
      edge.sizeDelta = new Vector2(direction.magnitude + unit, 1.75f * unit);
      edge.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
    }
    Rect bounds = Target.Footprint.Bounds;
    float gap = 5 * unit;
    float x = Mathf.Clamp(bounds.center.x - _tag.rect.width * .5f, gap, _session.ViewWidth - _tag.rect.width - gap);
    float y = bounds.yMin - lift - gap - _tag.rect.height;
    if (y < gap)
      y = bounds.yMax - lift + gap;
    Rect toolbar = _toolbar();
    if (new Rect(x, y, _tag.rect.width, _tag.rect.height).Overlaps(toolbar))
    {
      float above = toolbar.yMin - gap - _tag.rect.height;
      float below = toolbar.yMax + gap;
      y =
        above >= gap && (bounds.center.y < toolbar.center.y || below + _tag.rect.height > _session.ViewHeight)
          ? above
          : below;
    }
    _tag.anchoredPosition = new Vector2(x, -Mathf.Clamp(y, gap, _session.ViewHeight - _tag.rect.height - gap));
  }
}
