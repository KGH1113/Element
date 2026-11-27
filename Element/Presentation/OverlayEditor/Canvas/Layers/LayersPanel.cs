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
/// The layers panel's place: above the toolbar from its Order button, open
/// until that button closes it, without blocking the canvas. It keeps each
/// shown card's frame for carrying and pointing, and the cards' pictures.
/// What it shows is <see cref="LayersView"/>.
/// </summary>
internal sealed class LayersPanel : IPopover
{
  public const float Width = 392;

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
    ViewModel = new LayersViewModel(_session, _selection, _kinds, canvas, _toolbar.ViewModel.Floating);
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

  /// <summary>Above the toolbar from its Order button.</summary>
  public void Place()
  {
    if (_panel == null)
      return;
    float height = _panel.Size.y;
    PopoverPlacement.PlaceScaled(_panel.Root, _toolbar.PopoverAboveLayers(Width, height), Width, height);
  }

  /// <summary>Where it stands, in screen pixels from the top left; empty while closed.</summary>
  public Rect Bounds => Visible ? PopoverPlacement.ScreenRect(_panel.Root) : default;

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
