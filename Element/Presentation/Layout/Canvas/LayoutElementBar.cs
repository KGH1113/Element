using System.Collections.Generic;
using Element.Application.Layout;
using Element.Presentation.Layout.Kinds;
using Element.Presentation.Ui.Graphics;
using Element.Presentation.Ui.Popovers;
using Element.Presentation.Ui.Theme;
using Element.Presentation.Ui.Widgets;
using UnityEngine;
using UnityEngine.UI;

namespace Element.Presentation.Layout.Canvas;

/// <summary>
/// A second toolbar that follows the selection: what an element's kind
/// offers for it alone (hand or foot for a key viewer), or align and
/// distribute for several. It drags by its grip like the main toolbar and
/// keeps its place; it never blocks the canvas. An element sized in its
/// editor has none: Edit, a double-click or Enter opens its editor.
/// </summary>
internal sealed class LayoutElementBar : IPopover
{
  public const float Height = 56;
  private readonly LayoutSession _session;
  private readonly LayoutSelection _selection;
  private readonly LayoutToolbar _toolbar;
  private readonly ElementKinds _kinds;
  private readonly LayoutArrangeBar _arrange;
  private readonly List<RectTransform> _controls = new();
  private RectTransform _panel;
  private UiFactory _ui;
  private float _width,
    _height;
  private bool _stale;
  private bool _placed,
    _dragging;
  private Vector2 _position,
    _grab;

  public LayoutElementBar(
    LayoutSession session,
    LayoutSelection selection,
    LayoutToolbar toolbar,
    ElementKinds kinds,
    LayoutStatusLine status,
    ILayoutEditing editing
  )
  {
    _session = session;
    _selection = selection;
    _toolbar = toolbar;
    _kinds = kinds;
    _arrange = new LayoutArrangeBar(this, session, selection, status, editing);
  }

  /// <summary>The element it was built for; null for several.</summary>
  public IOverlayLayoutTarget Target { get; set; }

  /// <summary>The open bar's factory.</summary>
  public UiFactory Ui => _ui;

  public RectTransform Root => _panel;

  public bool Visible => _panel != null && _panel.gameObject.activeSelf;

  public bool IsTyping => false;

  public RectTransform Anchor => null;

  public bool Contains(Vector2 screen) => Visible && PopoverPlacement.Contains(_panel, screen);

  public void Hide() => Close();

  public bool Dragging
  {
    get => _dragging;
    set => _dragging = value;
  }

  /// <summary>Builds the bar again on the next frame: the selection's settings changed.</summary>
  public void MarkStale() => _stale = true;

  /// <summary>
  /// Once a frame: the bar for what is selected, built again when that
  /// changed. <paramref name="covered"/> closes it (a dialog or editor is
  /// open); <paramref name="hidden"/> hides it for now.
  /// </summary>
  public void Sync(bool covered, bool hidden)
  {
    IOverlayLayoutTarget target = _selection.Primary;
    if (target == null || covered)
    {
      Close();
      return;
    }
    if (_selection.Multi)
    {
      if (!_arrange.Built || _stale)
      {
        _stale = false;
        _arrange.Build();
      }
      _arrange.Refresh();
    }
    else if (Target != target || _stale || _arrange.Built)
    {
      _stale = false;
      if (!_kinds.BuildBar(this, target))
        Close();
    }
    if (_panel == null)
      return;
    if (_panel.gameObject.activeSelf == hidden)
      _panel.gameObject.SetActive(!hidden);
    Place();
  }

  /// <summary>Whether <paramref name="point"/> (screen pixels from the top left) is on the shown bar.</summary>
  public bool BoundsContain(Vector2 point) => Visible && Bounds.Contains(point);

  // Above the main toolbar until it is dragged somewhere else.
  private Rect Bounds
  {
    get
    {
      float scale = _session.ToolbarScale;
      float width = _width * scale,
        height = _height * scale;
      Rect toolbar = _toolbar.Bounds;
      Vector2 position = _placed ? _position : new Vector2(toolbar.center.x - width * .5f, toolbar.y - height - 10);
      if (!_placed && position.y < 0)
        position.y = toolbar.yMax + 10;
      position.x = Mathf.Clamp(position.x, 0, Mathf.Max(0, _session.ViewWidth - width));
      position.y = Mathf.Clamp(position.y, 0, Mathf.Max(0, _session.ViewHeight - height));
      return new Rect(position, new Vector2(width, height));
    }
  }

  /// <summary>Whether a control, which takes the press itself, is under <paramref name="point"/>.</summary>
  public bool ControlAt(Vector2 point)
  {
    Vector2 screen = new(point.x, _session.ViewHeight - point.y);
    foreach (RectTransform control in _controls)
      if (
        control != null
        && control.gameObject.activeInHierarchy
        && RectTransformUtility.RectangleContainsScreenPoint(control, screen, null)
      )
        return true;
    return false;
  }

  public void BeginDrag(Vector2 pointer)
  {
    _position = Bounds.position;
    _placed = _dragging = true;
    _grab = pointer - _position;
  }

  public void Move(Vector2 pointer)
  {
    _position = pointer - _grab;
    _position = Bounds.position;
    Place();
  }

  /// <summary>A new, empty bar for <paramref name="target"/> (null for several), with its grip.</summary>
  public RectTransform Begin(IOverlayLayoutTarget target, float width)
  {
    Close();
    Target = target;
    _width = width;
    _height = Height;
    _ui = _session.NewUi();
    Image panel = _ui.Panel("Element toolbar", _session.Root.transform, 0, 0, width, Height, UiPalette.Toolbar);
    panel.raycastTarget = true;
    _panel = panel.rectTransform;
    UiIcon grip = _ui.Rect("Drag element toolbar", _panel, 10, 16, 14, 24).gameObject.AddComponent<UiIcon>();
    grip.Kind = UiIconKind.Grip;
    grip.color = UiPalette.Muted;
    grip.raycastTarget = false;
    return _panel;
  }

  /// <summary>Registers <paramref name="control"/>: the bar doesn't drag from it.</summary>
  public T Control<T>(T control)
    where T : Component
  {
    _controls.Add((RectTransform)control.transform);
    return control;
  }

  public void AddControl(RectTransform control) => _controls.Add(control);

  public void Divider(float x) => _ui.Panel("Divider", _panel, x, 16, 1, 24, new Color(1, 1, 1, .1f), false);

  public void Place()
  {
    if (_panel != null)
      PopoverPlacement.PlaceScaled(_panel, Bounds, _width, _height);
  }

  /// <summary>Over the editor's other panels, shown or not.</summary>
  public void Raise() => _panel?.SetAsLastSibling();

  public void Close()
  {
    _dragging = false;
    if (_panel == null)
      return;
    _panel.gameObject.SetActive(false);
    UiFactory.Destroy(_panel.gameObject);
    _panel = null;
    _ui?.Dispose();
    _ui = null;
    Target = null;
    _arrange.Reset();
    _controls.Clear();
  }
}
