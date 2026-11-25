using Element.Presentation.OverlayEditor.Parts.Formula;
using Element.Presentation.OverlayEditor.Parts.Field;
using System;
using Element.Configuration.Overlay;
using Element.Domain.Overlay.Variables;
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
/// opacity as a number like W and H. While the picker is open the dock
/// points at the color's cases (<see cref="ColorCases"/>).
/// </summary>
internal sealed class ShapeColors
{
  private readonly IShapeEditorActions _editor;
  private readonly IShapeEditorContext _context;
  private readonly ShapeNumbers _numbers;
  private readonly ColorPickerPanel _picker;
  private PropertyButtonViewModel _fillOpacity,
    _strokeOpacity;
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
    _history = history;
    Cases = new ColorCases(_picker, () => Picked, WriteColor, _editor.Render);
  }

  private readonly EditorHistory<OverlayShapeSettings> _history;

  /// <summary>The picked color's cases, which the dock types while the picker is open.</summary>
  public ColorCases Cases { get; }

  /// <summary>The dock points at the picked color's cases: the picker is open.</summary>
  public bool CasesOpen => _picker.Picker != null && _picker.Picker.Visible;

  /// <summary>After every change: the picker edits a case's color while the formula being typed ends in one.</summary>
  public void Follow() => Cases.Follow(_numbers.Editing, () => Show(keepStop: true));

  // One undo step with the picker's changes.
  private void WriteColor(OverlayColorSettings color)
  {
    _picker.RememberChange();
    Picked = color;
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
  public string Title => Cases.OnCase ? ColorCases.CaseTitle : OwnTitle;

  /// <summary>The color the picker belongs to, as the dock names it: the fill or the border.</summary>
  public string OwnTitle => _pickerForStroke ? L("테두리 색", "Border color") : L("채우기 색", "Fill color");

  /// <summary>The picker's opacity edits the color it shows.</summary>
  public string OpacityKey => _pickerForStroke ? StrokeOpacity : FillOpacity;

  private const string FillOpacity = "fill-opacity",
    StrokeOpacity = "stroke-opacity";

  /// <summary>The column's Fill row; made by <see cref="BuildFill"/>.</summary>
  public PropertyButtonViewModel FillButton => _fillButton;

  /// <summary>The column's Border row; made by <see cref="BuildStroke"/>.</summary>
  public PropertyButtonViewModel StrokeButton => _strokeButton;

  /// <summary>The column's fill opacity row; made by <see cref="BuildFillOpacity"/>.</summary>
  public PropertyButtonViewModel FillOpacityButton => _fillOpacity;

  /// <summary>The column's border opacity row; made by <see cref="BuildStrokeOpacity"/>.</summary>
  public PropertyButtonViewModel StrokeOpacityButton => _strokeOpacity;

  // The fill's group: its color, then its opacity.
  public PropertyButtonViewModel BuildFill()
  {
    _fillButton = new PropertyButtonViewModel(
      "Shape fill",
      L("색", "Color"),
      () => Open(false),
      opens: true,
      field: OverlayValueType.Color
    );
    _fillButton.Section.Value = L("채우기", "Fill");
    return _fillButton;
  }

  public PropertyButtonViewModel BuildFillOpacity() =>
    _fillOpacity = new PropertyButtonViewModel(
      "Fill opacity",
      L("불투명도", "Opacity"),
      () => _editor.Activate(FillOpacity),
      field: OverlayValueType.Number
    );

  // The border's group: its color, its width (the numbers' row), then its opacity.
  public PropertyButtonViewModel BuildStroke()
  {
    _strokeButton = new PropertyButtonViewModel(
      "Shape border",
      L("색", "Color"),
      () => Open(true),
      opens: true,
      field: OverlayValueType.Color
    );
    _strokeButton.Section.Value = L("테두리", "Border");
    return _strokeButton;
  }

  public PropertyButtonViewModel BuildStrokeOpacity() =>
    _strokeOpacity = new PropertyButtonViewModel(
      "Border opacity",
      L("불투명도", "Opacity"),
      () => _editor.Activate(StrokeOpacity),
      field: OverlayValueType.Number
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
    Cases.ShowingOwn();
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
    ShowOpacity(_fillOpacity, FillOpacity, _context.Shape.Fill);
    ShowOpacity(_strokeOpacity, StrokeOpacity, _context.Shape.Stroke);
    _fillButton.ShowField(
      FieldChips.Of(_context.Shape.Fill, preview.FillShown, preview.FillGradient),
      Picker.Visible && !_pickerForStroke
    );
    _strokeButton.ShowField(
      FieldChips.Of(_context.Shape.Stroke, preview.StrokeShown, preview.StrokeGradient),
      Picker.Visible && _pickerForStroke
    );
  }

  // An opacity row: what it holds, and what that works out to in percent.
  private void ShowOpacity(PropertyButtonViewModel row, string key, OverlayColorSettings color)
  {
    OverlayVariable formula = _numbers.Shown(key);
    double percent = _numbers.Result(formula, (color?.Alpha ?? 1) * 100);
    row.ShowField(formula, FormulaFieldView.Number(percent) + " %", _numbers.Field == key);
  }

  /// <summary>The picker in the property column from <paramref name="top"/>, above the keypad.</summary>
  public void PlacePicker(InputDockPanel dock, float top) => _picker.Place(dock, top);

  /// <summary>The picker's opacity shows its result like W and H; a picked gradient color's own.</summary>
  public void ShowOpacity(OverlayGradientStop pickedStop)
  {
    if (!Picker.Visible)
      return;
    string key = OpacityKey;
    OverlayVariable formula = _numbers.Shown(key);
    OverlayColorSettings color = _pickerForStroke ? _context.Shape.Stroke : _context.Shape.Fill;
    Opacity.Show(
      formula,
      _numbers.Result(formula, (color?.Alpha ?? 1) * 100),
      pickedStop?.Alpha ?? color?.Alpha ?? 1,
      _numbers.Field == key
    );
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
