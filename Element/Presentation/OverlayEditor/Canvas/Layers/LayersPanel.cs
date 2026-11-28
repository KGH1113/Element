using System;
using System.Collections.Generic;
using Element.Presentation.OverlayEditor.Canvas.Toolbar;
using Element.Presentation.OverlayEditor.Kinds;
using Element.Presentation.Ui.Declarative;
using Element.Presentation.Ui.Popovers;
using Element.Presentation.Ui.Theme;
using UnityEngine;

namespace Element.Presentation.OverlayEditor.Canvas.Layers;

/// <summary>
/// The order panel's place: a panel of its own, carried by its head like the
/// toolbar, at the view's top right until it is moved and then where it was
/// left, open until the toolbar's Order button closes it, without blocking
/// the canvas. It keeps each shown card's frame for carrying and pointing,
/// and the cards' pictures. What it shows is <see cref="LayersView"/>.
/// </summary>
internal sealed class LayersPanel : IPopover
{
  public const float Width = 392;

  // From the view's edges, and from the toolbar it opens clear of.
  private const float Margin = 12;

  private readonly CanvasSession _session;
  private readonly CanvasSelection _selection;
  private readonly ToolbarPanel _toolbar;
  private readonly ElementKinds _kinds;
  private readonly CanvasStatusLine _status;
  private readonly Dictionary<string, RectTransform> _cards = new();
  private readonly LayerThumbnails _thumbnails;
  private ScreenPanel _panel;
  private int _historyVersion = -1;
  private int _themeVersion = -1;
  private ICanvasActions _canvas;
  private LayerRowViewModel[] _rows;
  private bool _dragging,
    _placed;
  private Vector2 _position,
    _grab;
  private float _placedScale;

  public LayersPanel(
    CanvasSession session,
    CanvasSelection selection,
    ToolbarPanel toolbar,
    ElementKinds kinds,
    CanvasStatusLine status
  )
  {
    _session = session;
    _selection = selection;
    _toolbar = toolbar;
    _kinds = kinds;
    _status = status;
    _thumbnails = new LayerThumbnails(session);
  }

  /// <summary>What the open panel shows; null while it is closed.</summary>
  public LayersViewModel ViewModel { get; private set; }

  public RectTransform Root => _panel?.Root;

  public bool Visible => _panel != null && _panel.Root.gameObject.activeSelf;

  public RectTransform Anchor => null;

  public bool Contains(Vector2 screen) => Visible && PopoverPlacement.Contains(_panel.Root, screen);

  public void Hide() => Close();

  /// <summary>Opens the panel with the layout as it is now.</summary>
  public void Open(ICanvasActions canvas)
  {
    Close();
    _canvas = canvas;
    _themeVersion = UiTheme.Version;
    ViewModel = new LayersViewModel(_session, _selection, _kinds, canvas);
    ViewModel.Refresh();
    _panel = new ScreenPanel("Layers", _session.Root.transform, UiPalette.Toolbar, keyboard: false);
    _panel.Mount(
      _session.NewBackend(_status.Hint),
      new LayersView(ViewModel, Track, _thumbnails.Fill, new LayerDrop(ViewModel, _cards)),
      ViewModel,
      Width
    );
    _historyVersion = _session.History.Version;
    Place();
  }

  public void Close()
  {
    _dragging = false;
    if (_panel == null)
      return;
    ViewModel?.Hover(null);
    _panel.Dispose();
    _panel = null;
    ViewModel = null;
    _rows = null;
    _cards.Clear();
  }

  /// <summary>
  /// Once a frame while open: the cards follow the layout and what is
  /// picked, the pictures are drawn again after a change, and the card under
  /// the pointer outlines its elements on the canvas.
  /// </summary>
  public void Sync(bool hidden)
  {
    if (_panel == null)
      return;
    if (_panel.Root.gameObject.activeSelf == hidden)
      _panel.Root.gameObject.SetActive(!hidden);
    if (hidden)
    {
      _dragging = false;
      ViewModel.Hover(null);
      return;
    }
    // Drawn in the highlight color it was opened in; opened again in a new one.
    if (_themeVersion != UiTheme.Version && !ElementUI.DragSession.Active)
    {
      Open(_canvas);
      return;
    }
    ViewModel.Refresh();
    if (_historyVersion != _session.History.Version)
    {
      _historyVersion = _session.History.Version;
      _thumbnails.RefreshAll();
    }
    // The panel is as tall as its cards; measured again only when they change.
    if (_rows != ViewModel.Rows)
    {
      _rows = ViewModel.Rows;
      _panel.Refit(Width);
    }
    _panel.Sync();
    Place();
    ElementUI.Runtime.Probe();
  }

  /// <summary>
  /// Once a frame from the editor's pointer handling: the card under the
  /// pointer outlines its elements on the canvas.
  /// </summary>
  public void TrackPointer(Vector2 pointer)
  {
    if (_panel == null || ElementUI.DragSession.Active)
      return;
    ViewModel.Hover(CardUnder(pointer));
  }

  /// <summary>Where it stands: its own place, scaled to the view.</summary>
  public void Place()
  {
    if (_panel == null)
      return;
    PopoverPlacement.PlaceScaled(_panel.Root, Frame(), Width, _panel.Size.y);
  }

  /// <summary>Where it stands, in screen pixels from the top left; empty while closed.</summary>
  public Rect Bounds => Visible ? PopoverPlacement.ScreenRect(_panel.Root) : default;

  /// <summary>Whether it is being carried.</summary>
  public bool Dragging => _dragging;

  /// <summary>
  /// Whether <paramref name="pointer"/> (screen pixels from the top left) is on
  /// the head it is carried by, away from the head's button.
  /// </summary>
  public bool HeadAt(Vector2 pointer)
  {
    if (!Visible)
      return false;
    Rect frame = Bounds;
    float head = LayersView.HeadHeight * frame.width / Width;
    return frame.Contains(pointer)
      && pointer.y <= frame.y + head
      && !_panel.ControlAt(new Vector2(pointer.x, _session.ViewHeight - pointer.y));
  }

  /// <summary>Starts carrying it from <paramref name="pointer"/> (screen pixels from the top left).</summary>
  public void BeginDrag(Vector2 pointer)
  {
    Rect frame = Frame();
    _position = frame.position;
    // It keeps the size it had where it opened, so it doesn't jump under the pointer.
    _placedScale = frame.width / Width;
    _dragging = _placed = true;
    _grab = pointer - _position;
  }

  /// <summary>Carries it to <paramref name="pointer"/>; it ends when the button is let go.</summary>
  public void Move(Vector2 pointer, bool held)
  {
    _position = pointer - _grab;
    _position = Frame().position;
    _dragging = held;
    Place();
  }

  // Its place in screen pixels from the top left: at the top right until it
  // is moved, then where it was left, kept inside the view. Before it is
  // moved it stays clear of the toolbar, smaller if it must, and keeps that size once moved.
  private Rect Frame()
  {
    float viewWidth = _session.ViewWidth,
      viewHeight = _session.ViewHeight;
    float height = _panel.Size.y;
    float scale = Mathf.Min(_session.UiScale, Mathf.Min((viewWidth - 16) / Width, (viewHeight - 16) / height));
    float margin = Margin * _session.UiScale;
    Rect toolbar = _toolbar.Bounds;
    if (_placed)
      scale = Mathf.Min(scale, _placedScale);
    else if (toolbar.xMax > viewWidth - Width * scale - margin && toolbar.y > margin)
      scale = Mathf.Min(scale, (toolbar.y - margin * 2) / height);
    float w = Width * scale,
      h = height * scale;
    Vector2 p = _placed ? _position : new Vector2(viewWidth - w - margin, margin);
    p.x = Mathf.Clamp(p.x, 0, Mathf.Max(0, viewWidth - w));
    p.y = Mathf.Clamp(p.y, 0, Mathf.Max(0, viewHeight - h));
    return new Rect(p, new Vector2(w, h));
  }

  /// <summary>Over the editor's other panels.</summary>
  public void Raise() => _panel?.Root.SetAsLastSibling();

  // Keeps a shown card's frame by its key until it goes.
  private IDisposable Track(string key, RectTransform card)
  {
    _cards[key] = card;
    return new Untrack(_cards, key, card);
  }

  // pointer: screen pixels from the bottom left.
  private LayerRowViewModel CardUnder(Vector2 pointer)
  {
    if (!_panel.Root.gameObject.activeInHierarchy)
      return null;
    foreach (LayerRowViewModel row in ViewModel.Rows)
      if (
        _cards.TryGetValue(row.Key, out RectTransform card)
        && card != null
        && card.gameObject.activeInHierarchy
        && RectTransformUtility.RectangleContainsScreenPoint(card, pointer, null)
      )
        return row;
    return null;
  }

  private sealed class Untrack : IDisposable
  {
    private readonly Dictionary<string, RectTransform> _cards;
    private readonly string _key;
    private readonly RectTransform _card;

    public Untrack(Dictionary<string, RectTransform> cards, string key, RectTransform card)
    {
      _cards = cards;
      _key = key;
      _card = card;
    }

    public void Dispose()
    {
      if (_cards.TryGetValue(_key, out RectTransform card) && card == _card)
        _cards.Remove(_key);
    }
  }
}
