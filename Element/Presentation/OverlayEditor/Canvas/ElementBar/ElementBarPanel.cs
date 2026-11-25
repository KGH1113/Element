using Element.Application.Layout;
using Element.Presentation.OverlayEditor.Canvas;
using Element.Presentation.OverlayEditor.Canvas.Toolbar;
using Element.Presentation.OverlayEditor.Kinds;
using Element.Presentation.Ui.Declarative;
using Element.Presentation.Ui.Popovers;
using Element.Presentation.Ui.Theme;
using UnityEngine;

namespace Element.Presentation.OverlayEditor.Canvas.ElementBar;

/// <summary>
/// The frame of a second toolbar that follows the selection: what an
/// element's kind offers for it alone (hand or foot for a key viewer), or
/// align and distribute for several. It drags from anywhere but its controls
/// like the main toolbar and keeps its place; it never blocks the canvas. An
/// element sized in its editor has none: Edit, a double-click or Enter opens
/// its editor. What it shows is a <see cref="ElementBarContent"/>.
/// </summary>
internal sealed class ElementBarPanel : IPopover
{
  private readonly CanvasSession _session;
  private readonly CanvasSelection _selection;
  private readonly ToolbarPanel _toolbar;
  private readonly ElementKinds _kinds;
  private readonly CanvasStatusLine _status;
  private readonly ICanvasActions _canvas;
  private ScreenPanel _panel;
  private ElementBarViewModel _model;
  private bool _stale;
  private bool _placed,
    _dragging;
  private Vector2 _position,
    _grab;
  private int _themeVersion = -1;

  public ElementBarPanel(
    CanvasSession session,
    CanvasSelection selection,
    ToolbarPanel toolbar,
    ElementKinds kinds,
    CanvasStatusLine status,
    ICanvasActions canvas
  )
  {
    _session = session;
    _selection = selection;
    _toolbar = toolbar;
    _kinds = kinds;
    _status = status;
    _canvas = canvas;
  }

  /// <summary>Where a panel above the toolbar stands, in screen pixels from the top left; empty for none.</summary>
  public System.Func<Rect> Avoid { get; set; }

  /// <summary>What the open bar shows; null while closed.</summary>
  public ElementBarViewModel Model => _model;

  public RectTransform Root => _panel?.Root;

  public bool Visible => _panel != null && _panel.Root.gameObject.activeSelf;

  public RectTransform Anchor => null;

  public bool Contains(Vector2 screen) => Visible && PopoverPlacement.Contains(_panel.Root, screen);

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
    // Drawn in the highlight color it was made in; made again in a new one when it shows.
    if (!hidden && _themeVersion != UiTheme.Version)
      _stale = true;
    if (_selection.Multi)
    {
      if (_model is not ArrangeViewModel || _stale)
        Show(ArrangeViewModel.Screen(_session, _selection, _status, _canvas));
    }
    else if (_model == null || _model.Target != target || _stale || _model is ArrangeViewModel)
    {
      ElementBarContent bar = _kinds.Bar(target);
      if (bar == null)
        Close();
      else
        Show(bar);
    }
    _stale = false;
    if (_panel == null)
      return;
    _panel.Sync();
    _model.Sync();
    if (_panel.Root.gameObject.activeSelf == hidden)
      _panel.Root.gameObject.SetActive(!hidden);
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
      Vector2 size = _panel != null ? _panel.Size : Vector2.zero;
      float width = size.x * scale,
        height = size.y * scale;
      Rect toolbar = _toolbar.Bounds;
      Vector2 position = _placed ? _position : new Vector2(toolbar.center.x - width * .5f, toolbar.y - height - 10);
      if (!_placed && position.y < 0)
        position.y = toolbar.yMax + 10;
      // Until it is dragged somewhere, it stands clear of a panel opened above the toolbar.
      Rect avoid = !_placed && Avoid != null ? Avoid() : default;
      if (avoid.width > 0 && new Rect(position, new Vector2(width, height)).Overlaps(avoid))
        position.x = avoid.x - width - 10 >= 0 ? avoid.x - width - 10 : avoid.xMax + 10;
      position.x = Mathf.Clamp(position.x, 0, Mathf.Max(0, _session.ViewWidth - width));
      position.y = Mathf.Clamp(position.y, 0, Mathf.Max(0, _session.ViewHeight - height));
      return new Rect(position, new Vector2(width, height));
    }
  }

  /// <summary>Whether a control, which takes the press itself, is under <paramref name="point"/> (screen pixels from the top left).</summary>
  public bool ControlAt(Vector2 point) =>
    _panel != null && _panel.ControlAt(new Vector2(point.x, _session.ViewHeight - point.y));

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

  // A new bar in place of the open one; its view model goes with it.
  private void Show(ElementBarContent bar)
  {
    Close();
    _model = bar.Model;
    _panel = new ScreenPanel("Element toolbar", _session.Root.transform, UiPalette.Toolbar, keyboard: false);
    _panel.Mount(_session.NewBackend(_status.Hint), bar.View, bar.Model);
    _panel.Host.Root.Edited += bar.Model.Edited;
    _themeVersion = UiTheme.Version;
  }

  public void Place()
  {
    if (_panel != null)
      PopoverPlacement.PlaceScaled(_panel.Root, Bounds, _panel.Size.x, _panel.Size.y);
  }

  /// <summary>Over the editor's other panels, shown or not.</summary>
  public void Raise() => _panel?.Root.SetAsLastSibling();

  public void Close()
  {
    _dragging = false;
    if (_panel == null)
      return;
    _panel.Dispose();
    _panel = null;
    _model = null;
  }
}
