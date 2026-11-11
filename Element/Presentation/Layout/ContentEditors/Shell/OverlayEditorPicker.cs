using System;
using Element.Configuration;
using Element.Presentation.Settings;
using TMPro;
using UnityEngine;
using Element.Configuration.Overlay;

namespace Element.Presentation.Layout.ContentEditors.Shell;

/// <summary>
/// The color picker of the text and shape editors: right above the input
/// dock's keypad, in its column, carrying the color's opacity as a number the
/// dock types. All changes while it is open are one undo step; a moved
/// slider replaces an opacity the dock is calculating, while a new color
/// keeps it. Closing it closes its numbers too.
/// </summary>
internal sealed class OverlayEditorPicker
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
  public OverlayEditorPicker(
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

  public SettingsColorPicker Picker { get; private set; }

  public OverlayOpacityField Opacity { get; private set; }

  /// <summary>The picker over <paramref name="frame"/>; then its opacity and its place above the keypad.</summary>
  public void Build(RectTransform frame)
  {
    Picker = new SettingsColorPicker(frame, _font, _reduceMotion);
    Picker.Hidden = Hidden;
  }

  /// <summary>The opacity as a number; a click types it in the dock.</summary>
  public void BuildOpacity(SettingsUiFactory ui, Action activate) =>
    Opacity = new OverlayOpacityField(ui, Picker, activate, _reduceMotion());

  /// <summary>The color picker opens right above the keypad, in its column.</summary>
  public void LayOutAbove(OverlayInputDock dock) => Picker.LayOutAsColumn(dock.KeypadArea.width);

  public void Place(OverlayInputDock dock)
  {
    if (dock != null)
      KeypadPicker.Place(Picker, dock);
  }

  public void Dispose()
  {
    SettingsColorPicker picker = Picker;
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
    OverlayFormulaEditing editing
  )
  {
    if (!_snapshotTaken)
    {
      _remember();
      _snapshotTaken = true;
    }
    // A gradient's stops keep their own calculated opacities.
    if (!color.IsGradient)
      color = Opacity.Keep(before, color);
    // A moved slider replaces the opacity the dock is calculating.
    if (typingOpacity && !OverlayOpacityField.Read(before).SameAs(OverlayOpacityField.Read(color)))
      editing.Deactivate();
    return color;
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
