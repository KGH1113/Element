using System;
using Element.Configuration.Overlay;
using Element.Presentation.OverlayEditor.Parts.Dock;
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
  }

  /// <summary>The opacity as a number; a click types it in the dock.</summary>
  public void BuildOpacity(UiFactory ui, Action activate) =>
    Opacity = new OpacityFieldView(ui, Picker, activate, _reduceMotion());

  /// <summary>The color picker opens right above the keypad, in its column.</summary>
  public void LayOutAbove(InputDockPanel dock) => Picker.LayOutAsColumn(dock.KeypadArea.width);

  /// <summary>In the property column from <paramref name="top"/> (frame units), above the keypad.</summary>
  public void Place(InputDockPanel dock, float top)
  {
    if (dock != null)
      ColorPickerPlacement.Place(Picker, dock, top);
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
    // A gradient's stops keep their own calculated opacities.
    if (!color.IsGradient)
      color = Opacity.Keep(before, color);
    // A moved slider replaces the opacity the dock is calculating.
    if (typingOpacity && !OpacityFieldView.Read(before).SameAs(OpacityFieldView.Read(color)))
      editing.Deactivate();
    return color;
  }

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
