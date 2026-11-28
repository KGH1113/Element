using System;
using Element.Configuration.Overlay;
using Element.Presentation.OverlayEditor.Parts.Dock;
using Element.Presentation.OverlayEditor.Parts.Dock.Keypad;
using Element.Presentation.OverlayEditor.Parts.Formula;
using Element.Presentation.Ui.ColorPicker;
using Element.Presentation.Ui.Widgets;
using TMPro;
using UnityEngine;

namespace Element.Presentation.OverlayEditor.Parts.ColorPicker;

/// <summary>
/// The color picker of the text and shape editors: right above the input
/// dock's keypad, in its column, carrying the color's opacity as a number the
/// dock types. All changes while it is open are one undo step; a moved
/// slider replaces an opacity the dock is calculating, while a new color
/// keeps it. Closing it closes its numbers too.
/// </summary>
internal sealed class ColorPickerPanel
{
  private readonly TMP_FontAsset _font;
  private readonly Func<bool> _reduceMotion;
  private readonly Action _remember;
  private readonly Func<bool> _open;
  private readonly Func<bool> _typingOwnNumber;
  private readonly Action _closeNumbers;
  private readonly Action _render;
  private bool _snapshotTaken;

  /// <param name="remember">Takes the edit for undo, before the picker's first change.</param>
  /// <param name="open">Whether the editor is still open.</param>
  /// <param name="typingOwnNumber">Whether the dock types one of the picker's numbers.</param>
  /// <param name="closeNumbers">Closes what the dock types.</param>
  /// <param name="render">Redraws the editor.</param>
  public ColorPickerPanel(
    TMP_FontAsset font,
    Func<bool> reduceMotion,
    Action remember,
    Func<bool> open,
    Func<bool> typingOwnNumber,
    Action closeNumbers,
    Action render
  )
  {
    _font = font;
    _reduceMotion = reduceMotion;
    _remember = remember;
    _open = open;
    _typingOwnNumber = typingOwnNumber;
    _closeNumbers = closeNumbers;
    _render = render;
  }

  public UiColorPicker Picker { get; private set; }

  public OpacityFieldView Opacity { get; private set; }

  /// <summary>The picker over <paramref name="frame"/>; then its opacity and its place above the keypad.</summary>
  public void Build(RectTransform frame)
  {
    Picker = new UiColorPicker(frame, _font, _reduceMotion);
    Picker.Hidden = Hidden;
    // Highlight or custom is a color value picked in the dock below.
    Picker.Model.SourceInDock = true;
  }

  /// <summary>The opacity as a number; a click types it in the dock.</summary>
  public void BuildOpacity(UiFactory ui, Action activate) =>
    Opacity = new OpacityFieldView(ui, Picker, activate, _reduceMotion());

  /// <summary>
  /// A solid color is picked in the keypad's place, like any value's input
  /// method. A gradient's tool is as wide as the column: its color bar and
  /// numbers stand above the keypad, the picked color's square in the keypad's place.
  /// </summary>
  public void LayOutAbove(InputDockPanel dock)
  {
    Picker.UseCompact(KeypadView.Width, KeypadView.MethodHeight);
    Picker.LayOutAsColumn(dock.KeypadArea.width);
  }

  /// <summary>
  /// In the property column from <paramref name="top"/> (frame units), above
  /// the keypad. While <paramref name="typing"/> (a number is open on the
  /// keypad), a gradient's color square gives the keypad's place back.
  /// </summary>
  public void Place(InputDockPanel dock, float top, bool typing)
  {
    if (dock == null)
      return;
    // The keypad's place is lower while a condition is folded over it.
    Rect place = dock.MethodArea;
    Picker.UseCompact(place.width, place.height);
    if (Picker.InCompactPlace)
      ColorPickerPlacement.PlaceOnKeypad(Picker, dock);
    else
      ColorPickerPlacement.Place(Picker, dock, top);
    Picker.PlaneResting = typing;
  }

  public void Dispose()
  {
    UiColorPicker picker = Picker;
    Picker = null;
    picker?.Dispose();
  }

  /// <summary>The next change is its own undo step, as when the picker opens on another color.</summary>
  public void NewSession() => _snapshotTaken = false;

  /// <summary>
  /// A color the picker hands over, as the editor stores it in place of
  /// <paramref name="before"/>: a calculated opacity stays unless the opacity
  /// itself moved, and a moved opacity stops what the dock calculated for it.
  /// </summary>
  public OverlayColorSettings Changed(
    OverlayColorSettings before,
    OverlayColorSettings color,
    bool typingOpacity,
    FormulaEditing editing
  )
  {
    RememberOnce();
    // The picker knows nothing of the color's cases: they stay as they were.
    color.Formula = before?.Formula ?? string.Empty;
    // A gradient's stops keep their own calculated opacities.
    if (!color.IsGradient)
      color = Opacity.Keep(before, color);
    // A moved slider replaces the opacity the dock is calculating.
    if (typingOpacity && !OpacityFieldView.Read(before).SameAs(OpacityFieldView.Read(color)))
      editing.Deactivate();
    return color;
  }

  /// <summary>
  /// The dock changed the color the picker is open on: it is the same undo
  /// step as the picker's own changes since it opened.
  /// </summary>
  public void RememberChange() => RememberOnce();

  /// <summary>Takes the edit for undo before the first change since the picker opened.</summary>
  private void RememberOnce()
  {
    if (_snapshotTaken)
      return;
    _remember();
    _snapshotTaken = true;
  }

  // The color button lets go of its lit state once the picker closes.
  private void Hidden()
  {
    _snapshotTaken = false;
    if (!_open() || Picker == null)
      return;
    // Its numbers go with it.
    if (_typingOwnNumber())
      _closeNumbers();
    _render();
  }
}
