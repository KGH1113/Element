using Element.Presentation.Ui.Popovers;
using Element.Presentation.Ui.Widgets;
using UnityEngine;

namespace Element.Presentation.Layout.Canvas;

/// <summary>
/// The layout editor's popovers: the highlight color and the default font
/// above the toolbar, with the add menu and the element toolbar in one stack.
/// A press anywhere on the toolbar is not outside them.
/// </summary>
internal sealed class LayoutPopovers
{
  private readonly LayoutSession _session;
  private readonly LayoutToolbar _toolbar;
  private OverlayColorPopover _colorPopover;
  private OverlayFontPopover _fontPopover;
  private UiFactory _fontUi;

  public LayoutPopovers(LayoutSession session, LayoutToolbar toolbar)
  {
    _session = session;
    _toolbar = toolbar;
  }

  /// <summary>Hit tests in the editor's screen pixels, where its panels are placed.</summary>
  public PopoverStack Stack { get; } = new(InScreenRect);

  public OverlayColorPopover Color
  {
    get
    {
      if (_colorPopover == null)
      {
        _colorPopover = new OverlayColorPopover(_session.Root.transform, _session.Font, _session.Preferences);
        Stack.Add(_colorPopover);
      }
      return _colorPopover;
    }
  }

  /// <summary>The default font's list; made on first use, with the overlay fonts.</summary>
  public OverlayFontPopover Font(System.Action<string> picked)
  {
    if (_fontPopover == null)
    {
      _fontUi = _session.NewUi();
      _fontPopover = new OverlayFontPopover(
        _fontUi,
        _session.Root.transform,
        _session.Fonts,
        _session.Preferences.ReduceUiMotion,
        picked
      );
      Stack.Add(_fontPopover);
    }
    return _fontPopover;
  }

  public bool FontVisible => _fontPopover?.Visible ?? false;

  /// <summary>Applies typed values; false keeps the editor open to fix them.</summary>
  public bool Commit() => _colorPopover == null || _colorPopover.Commit();

  public void Tick() => _fontPopover?.Tick();

  /// <summary>Above the toolbar, flush with its right end.</summary>
  public void Place()
  {
    if (_colorPopover?.Visible ?? false)
      PopoverPlacement.PlaceScaled(
        _colorPopover.Root,
        _toolbar.PopoverAbove(OverlayColorPopover.Width, OverlayColorPopover.Height, 0, true),
        OverlayColorPopover.Width,
        OverlayColorPopover.Height
      );
    if (_fontPopover?.Visible ?? false)
    {
      float width = OverlayFontPopover.PreferredWidth,
        height = OverlayFontPopover.PreferredHeight;
      Rect bounds = _toolbar.PopoverAbove(width, height, 0, true);
      _fontPopover.Place(bounds.x, bounds.y, width, height);
      _fontPopover.Root.localScale = Vector3.one * (bounds.width / width);
    }
  }

  public void Dispose()
  {
    _colorPopover?.Dispose();
    _fontPopover?.Dispose();
    _fontUi?.Dispose();
  }

  private static bool InScreenRect(IPopover popover, Vector2 screen) =>
    PopoverPlacement.ScreenRect(popover.Root).Contains(new Vector2(screen.x, Screen.height - screen.y));
}
