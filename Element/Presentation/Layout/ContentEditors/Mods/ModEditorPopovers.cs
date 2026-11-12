using System;
using Element.Configuration.Overlay;
using Element.Presentation.Layout.ContentEditors.Shell;
using Element.Presentation.Overlay.Fonts;
using Element.Presentation.Overlay.Mods;
using Element.Presentation.Ui.ColorPicker;
using Element.Presentation.Ui.Popovers;
using Element.Presentation.Ui.Widgets;
using TMPro;
using UnityEngine;
using static Element.Presentation.Ui.Theme.UiLocale;

namespace Element.Presentation.Layout.ContentEditors.Mods;

/// <summary>
/// What a mod element's editing screen opens beside its parts: the color
/// picker, the full picker for paints, a dropdown's list, the font list and
/// Grow from. One is open at a time; the part that opened one closes it
/// again, and a press outside it closes it.
/// </summary>
internal sealed class ModEditorPopovers : IDisposable
{
  private readonly OverlayEditorFrame _screen;
  private readonly TMP_FontAsset _font;
  private readonly Func<bool> _reduceMotion;
  private readonly Func<OverlayFonts> _fonts;
  private readonly RectTransform _growButton;
  private readonly PopoverStack _stack = new();
  private readonly PickerPopover _picker;
  private readonly PickerPopover _paintPicker;
  private readonly ModListPopover _list;
  private readonly OverlayGrowPopover _grow;
  private ModColorRequest _pickerFor;
  private ModPaintRequest _paintFor;
  private UiFontPopover _fontPopover;
  private UiFactory _fontUi;
  private ModFontRequest _fontFor;

  public ModEditorPopovers(
    OverlayEditorFrame screen,
    TMP_FontAsset font,
    Func<bool> reduceMotion,
    Func<OverlayFonts> fonts,
    RectTransform growButton,
    Action<OverlayGrowth> pickGrowth
  )
  {
    _screen = screen;
    _font = font;
    _reduceMotion = reduceMotion;
    _fonts = fonts;
    _growButton = growButton;
    _picker = new PickerPopover(new UiColorPicker(screen.Frame, font, reduceMotion));
    // The full picker, laid out as the overlay editors' column so it offers gradients.
    _paintPicker = new PickerPopover(new UiColorPicker(screen.Frame, font, reduceMotion));
    _paintPicker.Picker.LayOutAsColumn(300);
    _list = new ModListPopover(screen.Ui, screen.Frame, reduceMotion());
    _grow = new OverlayGrowPopover(screen, pickGrowth) { Anchor = growButton };
    _stack.Add(_picker);
    _stack.Add(_paintPicker);
    _stack.Add(_list);
    _stack.Add(_grow);
  }

  public bool AnyOpen => _stack.AnyOpen;

  /// <summary>Whether a picker's field or the font search has the keyboard.</summary>
  public bool Typing => _stack.Typing;

  public bool GrowOpen => _grow.Visible;

  public void OpenColor(ModColorRequest request)
  {
    bool same = _picker.Visible && _pickerFor != null && _pickerFor.Anchor == request.Anchor;
    CloseAll();
    if (same)
      return;
    _pickerFor = request;
    _picker.Anchor = request.Anchor;
    _picker.Picker.Show(request.Title, request.Color, request.Opacity, request.Changed);
    Beside(_picker.Root, request.Anchor, _picker.Picker.CurrentWidth, _picker.Picker.Height);
  }

  public void OpenPaint(ModPaintRequest request)
  {
    bool same = _paintPicker.Visible && _paintFor != null && _paintFor.Anchor == request.Anchor;
    CloseAll();
    if (same)
      return;
    _paintFor = request;
    _paintPicker.Anchor = request.Anchor;
    _paintPicker.Picker.ShowSource(
      request.Title,
      ModPaints.ToSettings(request.Paint),
      color => request.Changed(ModPaints.FromSettings(color))
    );
    Beside(_paintPicker.Root, request.Anchor, _paintPicker.Picker.CurrentWidth, _paintPicker.Picker.Height);
  }

  public void OpenList(ModListRequest request)
  {
    bool same = _list.Visible && _list.Anchor == request.Anchor;
    CloseAll();
    if (same || request.Options == null || request.Options.Count == 0)
      return;
    _list.Show(request, _screen.FrameRect(request.Anchor), _screen.Width, _screen.Height);
  }

  public void OpenFont(ModFontRequest request)
  {
    bool same = _fontPopover != null && _fontPopover.Visible && _fontFor != null && _fontFor.Anchor == request.Anchor;
    CloseAll();
    OverlayFonts fonts = _fonts?.Invoke();
    if (same || fonts == null)
      return;
    if (_fontPopover == null)
    {
      _fontUi = new UiFactory(_font, _reduceMotion);
      _fontPopover = new UiFontPopover(_fontUi, _screen.Frame, fonts, _reduceMotion(), PickFont);
      _fontPopover.Hidden = () => _fontFor = null;
      _stack.Add(_fontPopover);
    }
    _fontFor = request;
    _fontPopover.Anchor = request.Anchor;
    _fontPopover.Show(
      request.Title,
      request.Font,
      L("기본 폰트", "Default font"),
      L("오버레이 기본 폰트를 따라가요", "Follows the overlay's default font")
    );
    Rect anchor = _screen.FrameRect(request.Anchor);
    float width = UiFontPopover.PreferredWidth,
      height = Mathf.Min(UiFontPopover.PreferredHeight, _screen.Height - 16);
    Rect bounds = PopoverPlacement.Beside(
      anchor,
      anchor.center.y - height * .5f,
      width,
      height,
      16,
      _screen.Width,
      _screen.Height
    );
    _fontPopover.Place(bounds.x, bounds.y, width, height);
    _fontPopover.Root.SetAsLastSibling();
  }

  private void PickFont(string key)
  {
    ModFontRequest request = _fontFor;
    _fontPopover?.Hide();
    _fontFor = null;
    request?.Picked(key);
  }

  // Beside the part that opened it, on the side with room, kept on screen.
  private void Beside(RectTransform popover, RectTransform anchorRect, float width, float height)
  {
    Rect anchor = _screen.FrameRect(anchorRect);
    PopoverPlacement.Place(
      popover,
      PopoverPlacement.Beside(anchor, anchor.center.y - height * .5f, width, height, 16, _screen.Width, _screen.Height)
    );
    popover.SetAsLastSibling();
  }

  /// <summary>Opens Grow from under its button, showing <paramref name="growth"/>, or closes it.</summary>
  public void ToggleGrow(OverlayGrowth growth, Anchor9 point)
  {
    if (_grow.Visible)
    {
      _grow.Hide();
      return;
    }
    CloseAll();
    _grow.Show(_screen.FrameRect(_growButton));
    _grow.Refresh(growth, point);
  }

  public void RefreshGrow(OverlayGrowth growth, Anchor9 point) => _grow.Refresh(growth, point);

  public void CloseAll()
  {
    _stack.CloseAll();
    _fontFor = null;
  }

  /// <summary>A click away from what is open, and from the part that opened it, closes it.</summary>
  public void PressOutside(Vector2 screen) => _stack.PressOutside(screen);

  /// <summary>Applies colors typed in an open picker; false keeps the screen open to fix them.</summary>
  public bool Commit() =>
    (!_picker.Visible || _picker.Picker.Commit()) && (!_paintPicker.Visible || _paintPicker.Picker.Commit());

  /// <summary>Over a page built again, so they open above it.</summary>
  public void Raise()
  {
    _picker.Root.SetAsLastSibling();
    _paintPicker.Root.SetAsLastSibling();
    _grow.Root.SetAsLastSibling();
  }

  public void Tick() => _fontPopover?.Tick();

  public void Dispose()
  {
    _pickerFor = null;
    _paintFor = null;
    _fontFor = null;
    _picker.Picker.Dispose();
    _paintPicker.Picker.Dispose();
    _list.Hide();
    _fontPopover?.Hide();
    _fontPopover = null;
    _fontUi?.Dispose();
    _fontUi = null;
  }
}
