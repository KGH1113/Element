using System;
using Element.Configuration;
using Element.Domain.Elements;
using Element.Presentation.Layout.ContentEditors.Shell;
using Element.Presentation.Overlay.Elements;
using Element.Presentation.Overlay.Shared;
using Element.Presentation.Settings;
using TMPro;
using UnityEngine;
using static Element.Presentation.Settings.SettingsLocale;

namespace Element.Presentation.Layout.ContentEditors.Text;

/// <summary>
/// The text's color: the bar's button with a swatch of the selection's
/// color (or the whole text's), and the color picker right above the
/// keypad, which carries the color's opacity as a number the dock types.
/// </summary>
internal sealed class TextColor
{
  private readonly ITextEditorHost _host;
  private readonly TextDocumentEditing _text;
  private readonly TextFormulas _formulas;
  private readonly TextGradient _gradient;
  private readonly OverlayValues _values;
  private readonly TMP_FontAsset _font;
  private readonly Func<bool> _reduceMotion;
  private readonly OverlayGradientFormulas _swatchFormulas = new();
  private readonly OverlayResolvedGradient _swatchGradient = new();
  private OverlayBarButton _colorButton;
  private SettingsColorPicker _picker;
  private OverlayOpacityField _opacity;
  private bool _pickerSnapshotTaken;

  public TextColor(
    ITextEditorHost host,
    TextDocumentEditing text,
    TextFormulas formulas,
    TextGradient gradient,
    OverlayValues values,
    TMP_FontAsset font,
    Func<bool> reduceMotion
  )
  {
    _host = host;
    _text = text;
    _formulas = formulas;
    _gradient = gradient;
    _values = values;
    _font = font;
    _reduceMotion = reduceMotion;
    OpacitySlot = new OverlayFormulaSlot(TextFormulas.OpacityKey, false, ReadOpacity, WriteOpacity);
  }

  public SettingsColorPicker Picker => _picker;

  public OverlayOpacityField Opacity => _opacity;

  public OverlayBarButton Button => _colorButton;

  /// <summary>How the keypad reads and writes the picker's opacity.</summary>
  public OverlayFormulaSlot OpacitySlot { get; }

  public OverlayBarButton BuildButton(SettingsUiFactory ui, Transform bar, bool reduceMotion) =>
    _colorButton = new OverlayBarButton(
      ui,
      bar,
      "Text color",
      L("글자 색", "Text color"),
      OpenPicker,
      reduceMotion,
      swatch: true
    );

  /// <summary>The picker over the controls, with its opacity as a number.</summary>
  public void BuildPicker(OverlayEditorFrame screen)
  {
    _picker = new SettingsColorPicker(screen.Frame, _font, _reduceMotion);
    _picker.Hidden = PickerHidden;
    _opacity = new OverlayOpacityField(
      screen.Ui,
      _picker,
      () => _formulas.Activate(TextFormulas.OpacityKey),
      _reduceMotion()
    );
  }

  /// <summary>The color picker opens right above the keypad, in its column.</summary>
  public void LayOutAbove(OverlayInputDock dock) => _picker.LayOutAsColumn(dock.KeypadArea.width);

  public void DisposePicker()
  {
    SettingsColorPicker picker = _picker;
    _picker = null;
    picker?.Dispose();
  }

  private void OpenPicker()
  {
    _host.ClosePopovers();
    if (_picker.Visible)
    {
      _picker.Hide();
      _host.Render();
      return;
    }
    _text.Range(out int start, out int end);
    OverlayColorSettings current = _text.RangeColor();
    _picker.ShowSource(
      _text.HasSelection ? L("선택한 글자 색", "Selected text color") : L("글자 색", "Text color"),
      current,
      color =>
      {
        if (!_pickerSnapshotTaken)
        {
          _text.Remember(false);
          _pickerSnapshotTaken = true;
        }
        OverlayColorSettings before = _text.RangeColor();
        // A gradient's stops keep their own calculated opacities.
        if (!color.IsGradient)
          color = _opacity.Keep(before, color);
        _text.SetColor(start, end, color);
        // A moved slider replaces the opacity the dock is calculating.
        if (
          _formulas.Field == TextFormulas.OpacityKey
          && !OverlayOpacityField.Read(before).SameAs(OverlayOpacityField.Read(color))
        )
          _formulas.Editing.Deactivate();
        _host.Render();
      }
    );
    _host.Render();
  }

  // The color button lets go of its lit state once the picker closes.
  private void PickerHidden()
  {
    _pickerSnapshotTaken = false;
    if (_host.Screen == null || _picker == null)
      return;
    // Its numbers go with it.
    if (_formulas.Field == TextFormulas.OpacityKey || TextGradient.IsKey(_formulas.Field))
      _host.ClosePopovers();
    _host.Render();
  }

  public void Place(OverlayInputDock dock)
  {
    if (dock != null)
      KeypadPicker.Place(_picker, dock);
  }

  public void ShowButton() => _colorButton.Set(null, false, _picker.Visible);

  /// <summary>The range's color in the bar's button: its swatch, or its gradient.</summary>
  public void ShowSwatch()
  {
    Color swatch = OverlayTextComposer.Resolve(_text.RangeColor(), OverlayTheme.Accent);
    swatch.a = 1;
    _colorButton.Swatch = swatch;
    OverlayColorSettings color = _text.RangeColor();
    _swatchFormulas.Compile(color);
    bool gradient = _swatchFormulas.Resolve(color, OverlayTheme.Accent, _values.WithSamples, 1, 1, _swatchGradient);
    _colorButton.SwatchGradient = gradient ? _swatchGradient : null;
  }

  // The picker's opacity shows its result like the size.
  public void ShowOpacity()
  {
    if (!_picker.Visible)
      return;
    bool opacity = _formulas.Edits(TextFormulas.OpacityKey, out OverlayFormula typed);
    OverlayFormula alpha = typed ?? ReadOpacity();
    OverlayColorSettings color = _text.RangeColor();
    _opacity.Show(alpha, _formulas.Result(alpha), _gradient.PickedStop(color)?.Alpha ?? color.Alpha, opacity);
  }

  /// <summary>The line in the dock while the picker is open; null while it is closed.</summary>
  public string Hint() =>
    _picker.Visible && _picker.IsGradient
      ? L(
        "글자 색을 고르는 중이에요 · 값을 불투명도·위치·각도 칸에 끌어 놓으면 그 값으로 계산해요",
        "Picking the text color · Drop a value on an opacity, position or angle to calculate it"
      )
    : _picker.Visible
      ? L(
        "글자 색을 고르는 중이에요 · 값을 불투명도 칸에 끌어 놓으면 그 값으로 계산해요",
        "Picking the text color · Drop a value on the opacity to calculate it"
      )
    : null;

  // A picked gradient color has its own opacity.
  private OverlayFormula ReadOpacity() =>
    _gradient.PickedStop(_text.RangeColor()) != null
      ? _gradient.ReadStop(false)
      : OverlayOpacityField.Read(_text.RangeColor());

  private void WriteOpacity(OverlayFormula formula)
  {
    if (_gradient.PickedStop(_text.RangeColor()) != null)
    {
      _gradient.WriteStop(false, formula);
      return;
    }
    _text.RememberFormula(TextFormulas.OpacityKey);
    _text.Range(out int start, out int end);
    OverlayColorSettings color = OverlayOpacityField.Write(_text.RangeColor(), formula);
    if (start < end)
      _text.Document.SetAlpha(start, end, color.Alpha, color.AlphaFormula);
    else
      _text.PendingColor = color;
  }
}
