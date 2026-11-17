using System;
using Element.Configuration.Overlay;
using Element.Domain.Overlay.Formula;
using Element.Presentation.Overlay.Text;
using Element.Presentation.OverlayEditor.Parts;
using Element.Presentation.OverlayEditor.Parts.ColorPicker;
using Element.Presentation.OverlayEditor.Parts.Dock;
using Element.Presentation.OverlayEditor.Parts.EditorFrame;
using Element.Presentation.OverlayEditor.Parts.Property;
using Element.Presentation.Ui.ColorPicker;
using Element.Presentation.Ui.Theme;
using TMPro;
using UnityEngine;
using static Element.Presentation.Ui.Theme.UiLocale;

namespace Element.Presentation.OverlayEditor.ShapeEditor;

/// <summary>
/// The fill's and the border's colors: their bar buttons with a swatch, and
/// the color picker right above the keypad, which carries the color's
/// opacity as a number like W and H.
/// </summary>
internal sealed class ShapeColors
{
  private readonly IShapeEditorActions _editor;
  private readonly IShapeEditorContext _context;
  private readonly ShapeNumbers _numbers;
  private readonly ColorPickerPanel _picker;
  private PropertyButtonViewModel _fillButton,
    _strokeButton;
  private bool _pickerForStroke;

  public ShapeColors(
    IShapeEditorActions editor,
    IShapeEditorContext context,
    ShapeNumbers numbers,
    EditorHistory<OverlayShapeSettings> history,
    TMP_FontAsset font,
    Func<bool> reduceMotion
  )
  {
    _editor = editor;
    _context = context;
    _numbers = numbers;
    _picker = new ColorPickerPanel(
      font,
      reduceMotion,
      () => history.Remember((string)null),
      () => _context.Frame != null,
      // Its numbers go with it.
      () => _numbers.Field != null && ShapeGradient.PickerKey(_numbers.Field),
      _editor.CloseOpen,
      _editor.Render
    );
  }

  public UiColorPicker Picker => _picker.Picker;

  public OpacityFieldView Opacity => _picker.Opacity;

  /// <summary>The color the picker shows: the fill's or the border's.</summary>
  public OverlayColorSettings Picked
  {
    get => _pickerForStroke ? _context.Shape.Stroke : _context.Shape.Fill;
    set
    {
      if (_pickerForStroke)
        _context.Shape.Stroke = value;
      else
        _context.Shape.Fill = value;
    }
  }

  /// <summary>Whether the picker shows the border's color.</summary>
  public bool ForStroke => _pickerForStroke;

  /// <summary>What the open picker colors, as the column's back row names it.</summary>
  public string Title => _pickerForStroke ? L("테두리 색", "Border color") : L("채우기 색", "Fill color");

  /// <summary>The picker's opacity edits the color it shows.</summary>
  public string OpacityKey => _pickerForStroke ? "stroke-opacity" : "fill-opacity";

  /// <summary>The column's Fill row; made by <see cref="BuildFill"/>.</summary>
  public PropertyButtonViewModel FillButton => _fillButton;

  /// <summary>The column's Border row; made by <see cref="BuildStroke"/>.</summary>
  public PropertyButtonViewModel StrokeButton => _strokeButton;

  public PropertyButtonViewModel BuildFill() =>
    _fillButton = new PropertyButtonViewModel(
      "Shape fill",
      L("채우기", "Fill"),
      () => Open(false),
      value: true,
      swatch: true,
      opens: true
    );

  public PropertyButtonViewModel BuildStroke() =>
    _strokeButton = new PropertyButtonViewModel(
      "Shape border",
      L("테두리", "Border"),
      () => Open(true),
      value: true,
      swatch: true,
      opens: true
    );

  /// <summary>The picker in the keypad's column, with its opacity as a number.</summary>
  public void BuildPicker(EditorFramePanel screen, InputDockPanel dock)
  {
    _picker.Build(screen.Frame);
    // The color picker opens right above the keypad.
    _picker.LayOutAbove(dock);
    _picker.BuildOpacity(screen.Ui, () => _editor.Activate(OpacityKey));
  }

  public void DisposePicker() => _picker.Dispose();

  // The same button closes its picker; the other one moves it over.
  private void Open(bool stroke)
  {
    _editor.CloseOpen();
    if (Picker.Visible && _pickerForStroke == stroke)
    {
      Picker.Hide();
      _editor.Render();
      return;
    }
    _pickerForStroke = stroke;
    _picker.NewSession();
    Show();
    _editor.Render();
  }

  /// <summary>
  /// Shows the fill's or the border's color; after undo, shows it again as
  /// restored, on the same gradient stop.
  /// </summary>
  public void Show(bool keepStop = false)
  {
    bool stroke = _pickerForStroke;
    Picker.ShowSource(
      Title,
      stroke ? _context.Shape.Stroke : _context.Shape.Fill,
      picked =>
      {
        OverlayShapeSettings shape = _context.Shape;
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
            shape.StrokeWidth = 4 / _context.ElementScale;
        }
        else
          shape.Fill = color;
        _editor.Render();
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
    _fillButton.SetSwatch(OverlayTextComposer.Resolve(preview.Resolved.Fill, UiTheme.Accent), preview.FillGradient);
    _fillButton.Set(ColorNames.Describe(_context.Shape.Fill), false, Picker.Visible && !_pickerForStroke);
    _strokeButton.SetSwatch(
      OverlayTextComposer.Resolve(preview.Resolved.Stroke, UiTheme.Accent),
      preview.StrokeGradient
    );
    _strokeButton.Set(ColorNames.Describe(_context.Shape.Stroke), false, Picker.Visible && _pickerForStroke);
  }

  /// <summary>The picker in the property column from <paramref name="top"/>, above the keypad.</summary>
  public void PlacePicker(InputDockPanel dock, float top) => _picker.Place(dock, top);

  /// <summary>The picker's opacity shows its result like W and H; a picked gradient color's own.</summary>
  public void ShowOpacity(OverlayGradientStop pickedStop)
  {
    if (!Picker.Visible)
      return;
    string key = OpacityKey;
    OverlayFormula formula = _numbers.Shown(key);
    OverlayColorSettings color = _pickerForStroke ? _context.Shape.Stroke : _context.Shape.Fill;
    Opacity.Show(formula, _numbers.Result(formula), pickedStop?.Alpha ?? color?.Alpha ?? 1, _numbers.Field == key);
  }

  /// <summary>The line in the dock while the picker is open; null while it is closed.</summary>
  public string Hint()
  {
    if (!Picker.Visible)
      return null;
    if (Picker.Gradient.IsGradient)
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
