using System;
using Element.Configuration.Overlay;
using Element.Presentation.OverlayEditor.Parts.Dock.Keypad;
using Element.Presentation.OverlayEditor.Parts.EditorFrame;
using Element.Presentation.Ui.Declarative;
using Element.Presentation.Ui.Popovers;
using Element.Presentation.Ui.Theme;
using UnityEngine;

namespace Element.Presentation.OverlayEditor.Parts.Grow;

/// <summary>
/// Which point of the element stays put when it changes size: a panel under
/// the Grow from button showing <see cref="GrowPopoverView"/>, or, in an
/// editor with an input dock, the point field's input method
/// (<see cref="GrowKeysView"/>) standing where the keypad does. Made with
/// the editor's screen and shown or hidden.
/// </summary>
internal sealed class GrowPopoverPanel : IPopover
{
  private const float PopoverWidth = 312;

  private readonly EditorFramePanel _screen;
  private readonly ScreenPanel _panel;
  private readonly float _width;
  private Rect _anchor;
  private Vector2? _place;

  /// <param name="picked">A point or Auto was clicked; show the choice with <see cref="Refresh"/>.</param>
  /// <param name="asInputMethod">It stands in an input dock, where the keypad does, without a panel of its own.</param>
  public GrowPopoverPanel(EditorFramePanel screen, Action<OverlayGrowth> picked, bool asInputMethod = false)
  {
    _screen = screen;
    _width = asInputMethod ? KeypadView.Width : PopoverWidth;
    ViewModel = new GrowPickerViewModel(picked);
    _panel = screen.Panel("Grow popover", asInputMethod ? Color.clear : UiPalette.Popover);
    _panel.Mount(
      screen.NewBackend(),
      asInputMethod ? new GrowKeysView(ViewModel) : new GrowPopoverView(ViewModel),
      ViewModel,
      _width
    );
    Root.gameObject.SetActive(false);
  }

  /// <summary>What it shows: the points, Auto and the caption.</summary>
  public GrowPickerViewModel ViewModel { get; }

  public RectTransform Root => _panel.Root;

  public bool Visible => Root.gameObject.activeSelf;

  /// <summary>Opens below <paramref name="anchor"/> (frame units), kept on the editor's screen.</summary>
  public void Show(Rect anchor)
  {
    _anchor = anchor;
    _place = null;
    Root.gameObject.SetActive(true);
    Place();
    Root.SetAsLastSibling();
  }

  /// <summary>Stands in an input dock: its top left at <paramref name="at"/> (frame units), where the input method is drawn.</summary>
  public void ShowAt(Vector2 at)
  {
    _place = at;
    Root.gameObject.SetActive(true);
    Place();
    Root.SetAsLastSibling();
  }

  /// <summary>Shows <paramref name="growth"/>; with Auto, <paramref name="point"/> is today's point.</summary>
  public void Refresh(OverlayGrowth growth, Anchor9 point)
  {
    ViewModel.Show(growth, point);
    // The caption may take another line.
    _panel.Refit(_width);
    if (Visible)
      Place();
  }

  private void Place()
  {
    Vector2 size = _panel.Size;
    if (_place is { } place)
    {
      Root.anchoredPosition = new Vector2(Mathf.Round(place.x), -Mathf.Round(place.y));
      return;
    }
    Rect bounds = PopoverPlacement.Under(
      _anchor,
      _anchor.center.x - size.x * .5f,
      size.x,
      size.y,
      10,
      _screen.Width,
      _screen.Height
    );
    Root.anchoredPosition = new Vector2(Mathf.Round(bounds.x), -Mathf.Round(bounds.y));
  }

  public void Hide()
  {
    if (Visible)
      Root.gameObject.SetActive(false);
  }

  /// <summary>The button that opens it, when its owner says so.</summary>
  public RectTransform Anchor { get; set; }

  public bool Contains(Vector2 screen) => PopoverPlacement.Contains(Root, screen);
}
