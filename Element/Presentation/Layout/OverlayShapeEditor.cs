using System;
using System.Collections.Generic;
using Element.Configuration;
using Element.Domain.Elements;
using Element.Domain.Layout;
using Element.Presentation.Overlay.Elements;
using Element.Presentation.Settings;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static Element.Presentation.Settings.SettingsLocale;

namespace Element.Presentation.Layout;

/// <summary>
/// Shape editing over a dimmed screen, like the text editor. The shape sits in
/// the middle at its in-game size with W under it and H beside it, and an arc
/// outside each corner for its rounding. Fill, border and where the shape
/// grows from float above. Clicking a number types it in the input dock along
/// the bottom, which is always there.
/// </summary>
internal sealed partial class OverlayShapeEditor : IDisposable
{
  private const float BarHeight = 40,
    MeasureGap = 22,
    RadiusOffset = 48,
    MaximumSize = 10000,
    MaximumStroke = 100;

  private readonly Transform _parent;
  private readonly TMP_FontAsset _font;
  private readonly Func<bool> _reduceMotion;
  private readonly OverlayValues _values;
  private readonly OverlayLiveValues _live;
  private readonly OverlayFormulaEditing _editing = new();
  private readonly OverlayShapeResolver _resolver = new();
  private readonly OverlayShapeSettings _resolved = new();
  private readonly List<OverlayShapeSettings> _undo = new();
  private readonly List<OverlayShapeSettings> _redo = new();
  private readonly List<OverlayFormulaField> _radiusFields = new();
  private readonly List<Button> _arcs = new();
  private readonly List<System.Numerics.Vector2> _corners = new(4);
  private readonly Event _event = new();
  private OverlayEditorFrame _screen;
  private SettingsUiFactory _ui;
  private RectTransform _frame;
  private RectTransform _shapeRect;
  private ShapeGraphic _graphic;
  private RectTransform _bar;
  private OverlayBarButton _fillButton,
    _strokeButton,
    _strokeWidth,
    _growButton;
  private bool _pickerForStroke;
  private OverlayOpacityField _opacity;
  private SettingsCapsuleGraphic _growMark;
  private OverlayFormulaField _widthField,
    _heightField;
  private OverlayInputDock _dock;
  private OverlayGrowPopover _growPopover;
  private SettingsColorPicker _picker;
  private ShapeOverlayElement _element;
  private Action<bool> _closed;
  private OverlayShapeSettings _shape;
  private OverlayShapeSettings _opening;
  private OverlayGrowth _growth;
  private float _elementScale = 1;
  private float _blinkStart;
  private float _lastAt = -1;
  private string _field;
  private string _lastKey;
  private bool _pickerSnapshotTaken;
  private OverlayValueDefinition _dragValue;
  private string _dropKey;

  public OverlayShapeEditor(Transform parent, TMP_FontAsset font, Func<bool> reduceMotion, OverlayValues values)
  {
    _parent = parent;
    _font = font;
    _reduceMotion = reduceMotion;
    _values = values;
    _live = new OverlayLiveValues(values);
  }

  public bool IsOpen => _screen != null;

  public void Raise() => _screen?.Root.transform.SetAsLastSibling();

  public void Open(ShapeOverlayElement element, Action<bool> closed)
  {
    Close(false, false);
    _element = element;
    _closed = closed;
    _elementScale = Mathf.Max(.01f, element.EffectiveScale);
    _shape = (element.Shape ?? OverlayShapeGeometry.Create()).Clone();
    OverlayShapeGeometry.Normalize(_shape);
    _opening = _shape.Clone();
    _growth = element.Settings?.GrowFrom ?? OverlayGrowth.Auto;
    _undo.Clear();
    _redo.Clear();
    _field = null;
    _lastKey = null;
    _editing.Deactivate();
    Build();
    Render();
  }

  private bool HasCorners => OverlayShapeGeometry.HasCorners(_shape.Kind);

  private void Build()
  {
    SettingsColorPicker oldPicker = _picker;
    _picker = null;
    oldPicker?.Dispose();
    _screen?.Dispose();
    _radiusFields.Clear();
    _arcs.Clear();
    _dragValue = null;
    _screen = new OverlayEditorFrame(
      "Shape editor",
      _parent,
      _font,
      _reduceMotion,
      L(
        "도형 편집 · 숫자를 눌러 입력 · ⌘/Ctrl+Enter나 Esc로 완료",
        "Edit shape · Click a number to type · ⌘/Ctrl+Enter or Esc to finish"
      ),
      () => Close(false, true),
      () => Close(true, true)
    );
    _ui = _screen.Ui;
    _frame = _screen.Frame;

    _shapeRect = _ui.Rect("Shape", _screen.Stage, 0, 0, 10, 10);
    _graphic = _shapeRect.gameObject.AddComponent<ShapeGraphic>();
    _graphic.raycastTarget = false;
    _widthField = Field("Width", "width", "W");
    _heightField = Field("Height", "height", "H");
    int corners = OverlayShapeGeometry.DefaultPoints(_shape.Kind).Count;
    for (int i = 0; i < corners; i++)
    {
      int index = i;
      _arcs.Add(
        OverlayCornerArc.Create(
          _ui,
          _frame,
          L("꼭짓점 둥글기 ", "Corner rounding ") + (i + 1),
          () => Activate("radius:" + index),
          _reduceMotion()
        )
      );
      _radiusFields.Add(Field(L("꼭짓점 둥글기 값 ", "Corner radius ") + (i + 1), "radius:" + i, "R"));
    }

    BuildBar();
    _dock = new OverlayInputDock(
      _ui,
      _frame,
      _screen.Width,
      _screen.Height,
      _screen.Measure,
      _reduceMotion,
      KeypadPress,
      ClearFormula,
      FinishEditing,
      PickValue,
      BeginValueDrag,
      MoveValueDrag,
      EndValueDrag
    );
    _picker = new SettingsColorPicker(_frame, _font, _reduceMotion);
    _picker.Hidden = PickerHidden;
    // The color picker opens right above the keypad.
    _picker.LayOutAsColumn(_dock.KeypadArea.width);
    _opacity = new OverlayOpacityField(_ui, _picker, () => Activate(OpacityKey), _reduceMotion());
    _growPopover = new OverlayGrowPopover(_ui, _frame, _reduceMotion(), PickGrowth);
    _growMark = _ui.Capsule("Grow point", _frame, 0, 0, 12, 12, SettingsPalette.SoftAccent);
    _growMark.gameObject.SetActive(false);
  }

  private OverlayFormulaField Field(string name, string key, string prefix) =>
    new(_ui, _frame, name, "px", () => Activate(key), _reduceMotion(), prefix);

  // One row of labeled buttons: fill, border, border width and grow point.
  // Colors open their picker (with opacity) right under the button.
  private void BuildBar()
  {
    SettingsCapsuleGraphic surface = _ui.Capsule("Shape bar", _frame, 0, 0, 10, BarHeight, OverlayEditorUi.Bar);
    surface.raycastTarget = true;
    _bar = surface.rectTransform;
    bool motion = _reduceMotion();
    _fillButton = new OverlayBarButton(
      _ui,
      _bar,
      "Shape fill",
      L("채우기", "Fill"),
      () => OpenColor(false),
      motion,
      swatch: true
    );
    _strokeButton = new OverlayBarButton(
      _ui,
      _bar,
      "Shape border",
      L("테두리", "Border"),
      () => OpenColor(true),
      motion,
      swatch: true
    );
    _strokeWidth = new OverlayBarButton(
      _ui,
      _bar,
      "Border width",
      L("테두리 두께", "Border width"),
      () => Activate("stroke"),
      motion,
      "px",
      icon: LayoutAction.StrokeWidth
    );
    _growButton = new OverlayBarButton(
      _ui,
      _bar,
      "Grow from",
      L("기준점", "Grow from"),
      ToggleGrow,
      motion,
      icon: LayoutAction.GrowFrom
    );
  }

  private void OpenColor(bool stroke)
  {
    CloseEditing();
    // The same button closes its picker; the other one moves it over.
    if (_picker.Visible && _pickerForStroke == stroke)
    {
      _picker.Hide();
      Render();
      return;
    }
    _pickerForStroke = stroke;
    _pickerSnapshotTaken = false;
    ShowPicker();
    Render();
  }

  // Shows the fill's or the border's color; undo shows it again as restored.
  private void ShowPicker()
  {
    bool stroke = _pickerForStroke;
    _picker.ShowSource(
      stroke ? L("테두리 색", "Border color") : L("채우기 색", "Fill color"),
      stroke ? _shape.Stroke : _shape.Fill,
      color =>
      {
        if (!_pickerSnapshotTaken)
        {
          Remember(null);
          _pickerSnapshotTaken = true;
        }
        OverlayColorSettings before = stroke ? _shape.Stroke : _shape.Fill;
        color = _opacity.Keep(before, color);
        // A moved slider replaces the opacity the dock is calculating.
        if (_field == OpacityKey && !OverlayOpacityField.Read(before).SameAs(OverlayOpacityField.Read(color)))
          _editing.Deactivate();
        if (stroke)
        {
          _shape.Stroke = color;
          // A visible border needs a width; start from a thin one.
          if (_shape.StrokeWidth <= 0 && string.IsNullOrEmpty(_shape.StrokeWidthFormula))
            _shape.StrokeWidth = 4 / _elementScale;
        }
        else
          _shape.Fill = color;
        Render();
      }
    );
  }

  // Right under the button that opened it, like the grow popover.
  private void PlacePicker()
  {
    if (_dock != null)
      OverlayEditorUi.PlaceAboveKeypad(_picker, _dock);
  }

  // The color button lets go of its lit state once the picker closes.
  private void PickerHidden()
  {
    _pickerSnapshotTaken = false;
    if (_screen == null || _picker == null)
      return;
    // Its opacity goes with it.
    if (_field != null && Kind(_field, out _).EndsWith("opacity"))
      CloseEditing();
    Render();
  }

  /// <summary>Done writes the shape and where it grows from to the element; cancel leaves it as it was.</summary>
  public void Close(bool commit, bool notify)
  {
    if (_screen == null)
      return;
    if (commit && _picker.Visible && !_picker.Commit())
      return;
    CloseEditing();
    ShapeOverlayElement element = _element;
    Action<bool> closed = _closed;
    if (commit && element?.Settings != null)
    {
      if (!_shape.SameAs(_opening))
      {
        element.Settings.Shape = _shape.Clone();
        element.Apply();
      }
      element.SetGrowFrom(_growth);
    }
    SettingsColorPicker picker = _picker;
    _picker = null;
    picker?.Dispose();
    _screen.Dispose();
    _screen = null;
    _ui = null;
    _dragValue = null;
    _editing.Deactivate();
    _element = null;
    _closed = null;
    UIInteractionState.SetTextEditing(false);
    if (notify)
      closed?.Invoke(commit);
  }

  // Torn down with the editor open (the mod turned off), the edit is kept;
  // a color that can't be read is the only thing that falls back.
  public void Dispose()
  {
    Close(true, false);
    Close(false, false);
  }

  // Screen pixels per reference unit of the overlay canvas.
  private static float ScreenScale => OverlayLayoutPosition.ScreenScale(Screen.width, Screen.height);
}
