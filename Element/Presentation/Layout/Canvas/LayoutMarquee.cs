using System.Collections.Generic;
using Element.Domain.Layout;
using Element.Presentation.Settings;
using UnityEngine;
using UnityEngine.UI;

namespace Element.Presentation.Layout.Canvas;

/// <summary>
/// Dragging over empty space, or with Cmd/Ctrl or Shift anywhere, selects
/// what lies fully inside the area. A press is a marquee once it moves;
/// released without moving, a modifier click toggles what it hit and a
/// plain click on empty space clears the selection.
/// </summary>
internal sealed class LayoutMarquee
{
  private readonly LayoutSession _session;
  private readonly LayoutSelection _selection;
  private readonly LayoutCursors _cursors;
  private readonly List<IOverlayLayoutTarget> _base = new();
  private bool _pending,
    _active,
    _adds;
  private int _target = -1;
  private Vector2 _start;
  private Image _fill;
  private LayoutOutlineGraphic _outline;

  public LayoutMarquee(LayoutSession session, LayoutSelection selection, LayoutCursors cursors)
  {
    _session = session;
    _selection = selection;
    _cursors = cursors;
  }

  /// <summary>Whether a press is being followed.</summary>
  public bool Pending => _pending;

  /// <summary>The main toolbar; the area is drawn just under it.</summary>
  public RectTransform Toolbar { get; set; }

  /// <summary>A press at <paramref name="mouse"/>; <paramref name="target"/> is the element under it with a modifier, or -1.</summary>
  public void Begin(Vector2 mouse, int target, bool adds)
  {
    _pending = true;
    _active = false;
    _adds = adds;
    _target = target;
    _start = mouse;
    _base.Clear();
    if (adds)
      _base.AddRange(_selection.Targets);
  }

  public void Continue(Vector2 mouse)
  {
    _cursors.Set(LayoutHandle.None);
    if (!_active && (mouse - _start).sqrMagnitude > 9)
      _active = true;
    if (_active)
      SelectInside(mouse);
    if (Input.GetMouseButton(0))
      Draw(mouse);
    else
      Release();
  }

  public void Release()
  {
    if (!_active)
    {
      if (_target >= 0)
        _selection.Toggle(_target);
      else if (!_adds)
        _selection.SelectOnly(-1);
    }
    End();
  }

  public void End()
  {
    _pending = _active = false;
    _target = -1;
    _base.Clear();
    if (_fill != null && _fill.gameObject.activeSelf)
      _fill.gameObject.SetActive(false);
  }

  // Elements whose visible content lies fully inside the area join the
  // selection the press started with.
  private void SelectInside(Vector2 mouse)
  {
    Rect area = Area(mouse);
    List<IOverlayLayoutTarget> selection = _selection.Targets;
    List<LayoutFrame> frames = _session.Frames;
    selection.Clear();
    selection.AddRange(_base);
    int primary = selection.Count > 0 ? _session.IndexOf(selection[selection.Count - 1]) : -1;
    for (int i = 0; i < frames.Count; i++)
    {
      IOverlayLayoutTarget target = frames[i].Target;
      if (!LayoutSelection.Selectable(target) || selection.Contains(target))
        continue;
      Rect content = target.SelectionBounds;
      if (content.width <= 0 || content.height <= 0)
        continue;
      // Native viewers measure from their own window origin.
      Vector2 offset = LayoutSession.PointerFor(target, mouse) - mouse;
      Rect inSpace = new(area.position + offset, area.size);
      if (
        content.xMin >= inSpace.xMin
        && content.xMax <= inSpace.xMax
        && content.yMin >= inSpace.yMin
        && content.yMax <= inSpace.yMax
      )
      {
        selection.Add(target);
        primary = i;
      }
    }
    _selection.Selected = primary;
  }

  private void Draw(Vector2 mouse)
  {
    if (!_active)
      return;
    if (_fill == null)
    {
      _fill = _session.Ui.Panel("Selection area", _session.Root.transform, 0, 0, 10, 10, Color.white, false);
      _fill.raycastTarget = false;
      _fill.rectTransform.anchorMin = _fill.rectTransform.anchorMax = Vector2.zero;
      _fill.rectTransform.pivot = new Vector2(0, 1);
      _outline = _session
        .Ui.Rect("Selection area outline", _fill.transform, 0, 0, 10, 10)
        .gameObject.AddComponent<LayoutOutlineGraphic>();
      _outline.raycastTarget = false;
      SettingsUiFactory.Stretch(_outline.rectTransform, 0, 0, 0, 0);
    }
    Color accent = SettingsPalette.SoftAccent;
    _fill.color = new Color(accent.r, accent.g, accent.b, .06f);
    _outline.color = accent;
    _outline.SetScale(_session.UiScale * .6f);
    if (!_fill.gameObject.activeSelf)
      _fill.gameObject.SetActive(true);
    _session.SetRect(_fill.rectTransform, Area(mouse));
    // Under the toolbars, over the elements.
    _fill.transform.SetSiblingIndex(Mathf.Max(0, Toolbar.GetSiblingIndex() - 1));
  }

  private Rect Area(Vector2 mouse) =>
    Rect.MinMaxRect(
      Mathf.Min(_start.x, mouse.x),
      Mathf.Min(_start.y, mouse.y),
      Mathf.Max(_start.x, mouse.x),
      Mathf.Max(_start.y, mouse.y)
    );
}
