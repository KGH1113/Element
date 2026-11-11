using System;
using Element.Configuration;
using Element.Configuration.Overlay;
using Element.Domain.Overlay.Formula;
using Element.Presentation.Layout.ContentEditors.Shell;
using Element.Presentation.Overlay.Elements;
using Element.Presentation.Settings;
using Element.Presentation.Ui.Theme;
using Element.Presentation.Ui.Widgets;
using TMPro;
using UnityEngine;
using static Element.Presentation.Ui.Theme.UiLocale;

namespace Element.Presentation.Layout.ContentEditors.Shape;

/// <summary>
/// The fill's and the border's colors: their bar buttons with a swatch, and
/// the color picker right above the keypad, which carries the color's
/// opacity as a number like W and H.
/// </summary>
internal sealed class ShapeColors
{
  private readonly IShapeEditorHost _host;
  private readonly ShapeNumbers _numbers;
  private readonly OverlayEditorPicker _picker;
  private OverlayBarButton _fillButton,
    _strokeButton;
  private bool _pickerForStroke;

  public ShapeColors(
    IShapeEditorHost host,
    ShapeNumbers numbers,
    OverlayEditorHistory<OverlayShapeSettings> history,
    TMP_FontAsset font,
    Func<bool> reduceMotion
  )
  {
    _host = host;
    _numbers = numbers;
    _picker = new OverlayEditorPicker(
      font,
      reduceMotion,
      () => history.Remember((string)null),
      () => _host.Screen != null,
      // Its numbers go with it.
      () => _numbers.Field != null && ShapeGradient.PickerKey(_numbers.Field),
      _host.CloseEditing,
      _host.Render
    );
  }

  public SettingsColorPicker Picker => _picker.Picker;

  public OverlayOpacityField Opacity => _picker.Opacity;

  /// <summary>The color the picker shows: the fill's or the border's.</summary>
  public OverlayColorSettings Picked
  {
    get => _pickerForStroke ? _host.Shape.Stroke : _host.Shape.Fill;
    set
    {
      if (_pickerForStroke)
        _host.Shape.Stroke = value;
      else
        _host.Shape.Fill = value;
    }
  }

  /// <summary>Whether the picker shows the border's color.</summary>
  public bool ForStroke => _pickerForStroke;

  /// <summary>The picker's opacity edits the color it shows.</summary>
  public string OpacityKey => _pickerForStroke ? "stroke-opacity" : "fill-opacity";

  public OverlayBarButton BuildFill(UiFactory ui, Transform bar, bool reduceMotion) =>
    _fillButton = new OverlayBarButton(
      ui,
      bar,
      "Shape fill",
      L("채우기", "Fill"),
      () => Open(false),
      reduceMotion,
      swatch: true
    );

  public OverlayBarButton BuildStroke(UiFactory ui, Transform bar, bool reduceMotion) =>
    _strokeButton = new OverlayBarButton(
      ui,
      bar,
      "Shape border",
      L("테두리", "Border"),
      () => Open(true),
      reduceMotion,
      swatch: true
    );

  /// <summary>The picker in the keypad's column, with its opacity as a number.</summary>
  public void BuildPicker(OverlayEditorFrame screen, OverlayInputDock dock)
  {
    _picker.Build(screen.Frame);
    // The color picker opens right above the keypad.
    _picker.LayOutAbove(dock);
    _picker.BuildOpacity(screen.Ui, () => _host.Activate(OpacityKey));
  }

  public void DisposePicker() => _picker.Dispose();

  // The same button closes its picker; the other one moves it over.
  private void Open(bool stroke)
  {
    _host.CloseEditing();
    if (Picker.Visible && _pickerForStroke == stroke)
    {
      Picker.Hide();
      _host.Render();
      return;
    }
    _pickerForStroke = stroke;
    _picker.NewSession();
    Show();
    _host.Render();
  }

  /// <summary>
  /// Shows the fill's or the border's color; after undo, shows it again as
  /// restored, on the same gradient stop.
  /// </summary>
  public void Show(bool keepStop = false)
  {
    bool stroke = _pickerForStroke;
    Picker.ShowSource(
      stroke ? L("테두리 색", "Border color") : L("채우기 색", "Fill color"),
      stroke ? _host.Shape.Stroke : _host.Shape.Fill,
      picked =>
      {
        OverlayShapeSettings shape = _host.Shape;
        OverlayColorSettings color = _picker.Changed(
          stroke ? shape.Stroke : shape.Fill,
          picked,
          _numbers.Field == OpacityKey,
          _numbers.Editing
        );
        if (stroke)
        {
          shape.Stroke = color;
          // A visible border needs a width; start from a thin one.
          if (shape.StrokeWidth <= 0 && string.IsNullOrEmpty(shape.StrokeWidthFormula))
            shape.StrokeWidth = 4 / _host.ElementScale;
        }
        else
          shape.Fill = color;
        _host.Render();
      },
      keepStop
    );
  }

  /// <summary>Undo starts a new picker session: its next change is its own step.</summary>
  public void Restored()
  {
    _picker.NewSession();
    if (Picker.Visible)
      Show(keepStop: true);
  }

  /// <summary>The fill's and the border's buttons, each lit while its picker is open.</summary>
  public void ShowButtons(ShapePreview preview)
  {
    _fillButton.Swatch = Opaque(preview.Resolved.Fill);
    _fillButton.SwatchGradient = preview.FillGradient;
    _fillButton.Set(null, false, Picker.Visible && !_pickerForStroke);
    _strokeButton.Swatch = Opaque(preview.Resolved.Stroke);
    _strokeButton.SwatchGradient = preview.StrokeGradient;
    _strokeButton.Set(null, false, Picker.Visible && _pickerForStroke);
  }

  private static Color Opaque(OverlayColorSettings color)
  {
    Color resolved = OverlayTextComposer.Resolve(color, UiTheme.Accent);
    resolved.a = 1;
    return resolved;
  }

  public void PlacePicker(OverlayInputDock dock) => _picker.Place(dock);

  /// <summary>The picker's opacity shows its result like W and H; a picked gradient color's own.</summary>
  public void ShowOpacity(OverlayGradientStop pickedStop)
  {
    if (!Picker.Visible)
      return;
    string key = OpacityKey;
    OverlayFormula formula = _numbers.Shown(key);
    OverlayColorSettings color = _pickerForStroke ? _host.Shape.Stroke : _host.Shape.Fill;
    Opacity.Show(formula, _numbers.Result(formula), pickedStop?.Alpha ?? color?.Alpha ?? 1, _numbers.Field == key);
  }

  /// <summary>The line in the dock while the picker is open; null while it is closed.</summary>
  public string Hint()
  {
    if (!Picker.Visible)
      return null;
    if (Picker.IsGradient)
      return _pickerForStroke
        ? L(
          "테두리 색을 고르는 중이에요 · 값을 불투명도·위치·각도 칸에 끌어 놓으면 그 값으로 계산해요",
          "Picking the border color · Drop a value on an opacity, position or angle to calculate it"
        )
        : L(
          "채우기 색을 고르는 중이에요 · 값을 불투명도·위치·각도 칸에 끌어 놓으면 그 값으로 계산해요",
          "Picking the fill color · Drop a value on an opacity, position or angle to calculate it"
        );
    return _pickerForStroke
      ? L(
        "테두리 색을 고르는 중이에요 · 값을 불투명도 칸에 끌어 놓으면 그 값으로 계산해요",
        "Picking the border color · Drop a value on the opacity to calculate it"
      )
      : L(
        "채우기 색을 고르는 중이에요 · 값을 불투명도 칸에 끌어 놓으면 그 값으로 계산해요",
        "Picking the fill color · Drop a value on the opacity to calculate it"
      );
  }
}
