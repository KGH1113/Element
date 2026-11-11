using System;
using Element.Configuration;
using Element.Configuration.Overlay;
using Element.Domain.Overlay.Formula;
using Element.Presentation.Layout.ContentEditors.Shell;
using Element.Presentation.Overlay.Elements;
using Element.Presentation.Ui.ColorPicker;
using Element.Presentation.Ui.Theme;
using Element.Presentation.Ui.Widgets;
using TMPro;
using UnityEngine;
using static Element.Presentation.Ui.Theme.UiLocale;

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
  private readonly OverlayEditorPicker _picker;
  private readonly OverlayGradientFormulas _swatchFormulas = new();
  private readonly OverlayResolvedGradient _swatchGradient = new();
  private OverlayBarButton _colorButton;

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
    _picker = new OverlayEditorPicker(
      font,
      reduceMotion,
      () => _text.Remember(false),
      () => _host.Screen != null,
      // Its numbers go with it.
      () => _formulas.Field == TextFormulas.OpacityKey || TextGradient.IsKey(_formulas.Field),
      _host.ClosePopovers,
      _host.Render
    );
    OpacitySlot = new OverlayFormulaSlot(TextFormulas.OpacityKey, false, ReadOpacity, WriteOpacity);
  }

  public UiColorPicker Picker => _picker.Picker;

  public OverlayOpacityField Opacity => _picker.Opacity;

  public OverlayBarButton Button => _colorButton;

  /// <summary>How the keypad reads and writes the picker's opacity.</summary>
  public OverlayFormulaSlot OpacitySlot { get; }

  public OverlayBarButton BuildButton(UiFactory ui, Transform bar, bool reduceMotion) =>
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
    _picker.Build(screen.Frame);
    _picker.BuildOpacity(screen.Ui, () => _formulas.Activate(TextFormulas.OpacityKey));
  }

  /// <summary>The color picker opens right above the keypad, in its column.</summary>
  public void LayOutAbove(OverlayInputDock dock) => _picker.LayOutAbove(dock);

  public void DisposePicker() => _picker.Dispose();

  private void OpenPicker()
  {
    _host.ClosePopovers();
    UiColorPicker picker = _picker.Picker;
    if (picker.Visible)
    {
      picker.Hide();
      _host.Render();
      return;
    }
    _text.Range(out int start, out int end);
    OverlayColorSettings current = _text.RangeColor();
    picker.ShowSource(
      _text.HasSelection ? L("선택한 글자 색", "Selected text color") : L("글자 색", "Text color"),
      current,
      color =>
      {
        _text.SetColor(
          start,
          end,
          _picker.Changed(_text.RangeColor(), color, _formulas.Field == TextFormulas.OpacityKey, _formulas.Editing)
        );
        _host.Render();
      }
    );
    _host.Render();
  }

  public void Place(OverlayInputDock dock) => _picker.Place(dock);

  public void ShowButton() => _colorButton.Set(null, false, Picker.Visible);

  /// <summary>The range's color in the bar's button: its swatch, or its gradient.</summary>
  public void ShowSwatch()
  {
    Color swatch = OverlayTextComposer.Resolve(_text.RangeColor(), UiTheme.Accent);
    swatch.a = 1;
    _colorButton.Swatch = swatch;
    OverlayColorSettings color = _text.RangeColor();
    _swatchFormulas.Compile(color);
    bool gradient = _swatchFormulas.Resolve(color, UiTheme.Accent, _values.WithSamples, 1, 1, _swatchGradient);
    _colorButton.SwatchGradient = gradient ? _swatchGradient : null;
  }

  // The picker's opacity shows its result like the size.
  public void ShowOpacity()
  {
    if (!Picker.Visible)
      return;
    bool opacity = _formulas.Edits(TextFormulas.OpacityKey, out OverlayFormula typed);
    OverlayFormula alpha = typed ?? ReadOpacity();
    OverlayColorSettings color = _text.RangeColor();
    Opacity.Show(alpha, _formulas.Result(alpha), _gradient.PickedStop(color)?.Alpha ?? color.Alpha, opacity);
  }

  /// <summary>The line in the dock while the picker is open; null while it is closed.</summary>
  public string Hint() =>
    Picker.Visible && Picker.Gradient.IsGradient
      ? L(
        "글자 색을 고르는 중이에요 · 값을 불투명도·위치·각도 칸에 끌어 놓으면 그 값으로 계산해요",
        "Picking the text color · Drop a value on an opacity, position or angle to calculate it"
      )
    : Picker.Visible
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
