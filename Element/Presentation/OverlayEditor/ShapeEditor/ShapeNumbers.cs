using Element.Presentation.OverlayEditor.Parts.Field;
using System;
using System.Collections.Generic;
using Element.Configuration.Overlay;
using Element.Domain.Overlay.Shape;
using Element.Domain.Overlay.Values;
using Element.Domain.Overlay.Variables;
using Element.Presentation.Overlay.Values;
using Element.Presentation.OverlayEditor.Parts;
using Element.Presentation.OverlayEditor.Parts.ColorPicker;
using Element.Presentation.OverlayEditor.Parts.EditorFrame;
using Element.Presentation.OverlayEditor.Parts.Formula;
using Element.Presentation.OverlayEditor.Parts.Property;
using Element.Presentation.Ui.Graphics;
using UnityEngine;
using static Element.Presentation.Ui.Theme.UiLocale;
using NVector2 = System.Numerics.Vector2;

namespace Element.Presentation.OverlayEditor.ShapeEditor;

/// <summary>
/// The numbers of the shape the input dock types: W, H, the border width,
/// each corner's rounding, the picked color's opacity and its gradient's
/// numbers. Knows which one is open and the formula being typed there,
/// reads and writes them by key, and says when a typed number had to be
/// brought into range.
/// </summary>
internal sealed class ShapeNumbers
{
  public const float MaximumSize = 10000,
    MaximumStroke = 100;

  public const string StrokeKey = "stroke";

  private readonly IShapeEditorActions _editor;
  private readonly IShapeEditorContext _context;
  private readonly OverlayValues _values;
  private readonly EditorHistory<OverlayShapeSettings> _history;
  private readonly FormulaEditing _editing;
  private ShapeGradient _gradient;
  private PropertyButtonViewModel _strokeWidth;
  private FormulaSlot _slot;

  public ShapeNumbers(
    IShapeEditorActions editor,
    IShapeEditorContext context,
    OverlayValues values,
    EditorHistory<OverlayShapeSettings> history,
    ICustomValueBook book
  )
  {
    _editor = editor;
    _context = context;
    _values = values;
    _history = history;
    _editing = new FormulaEditing(book);
  }

  /// <summary>The key of the number open in the dock; null with none.</summary>
  public string Field { get; set; }

  public FormulaEditing Editing => _editing;

  /// <summary>The bar's Border width button; made by <see cref="BuildStrokeWidth"/>.</summary>
  public PropertyButtonViewModel StrokeWidth => _strokeWidth;

  /// <summary>The gradient reads and writes its own numbers.</summary>
  public void Use(ShapeGradient gradient) => _gradient = gradient;

  /// <param name="takesValue">A value carried onto the row: it calculates the width with it.</param>
  public PropertyButtonViewModel BuildStrokeWidth(Action<OverlayValueDefinition> takesValue) =>
    _strokeWidth = new PropertyButtonViewModel(
      "Border width",
      L("테두리 두께", "Border width"),
      () => _editor.Activate(StrokeKey),
      takesValue: takesValue,
      field: OverlayValueType.Number
    );

  /// <summary>
  /// Points the keypad at the open number; with none, at <paramref name="otherwise"/>
  /// (the open picker's color) or lets it rest.
  /// </summary>
  public void Sync(FormulaSlot otherwise = null) => _editing.Activate(Field == null ? otherwise : Slot(Field));

  /// <summary>The keypad's way to read and write the number of <paramref name="key"/>.</summary>
  public FormulaSlot Slot(string key)
  {
    if (_slot == null || _slot.Key != key)
      _slot = new FormulaSlot(
        key,
        OverlayValueType.Number,
        () => Read(key),
        formula => Write(key, formula),
        () => PlainText(key)
      );
    return _slot;
  }

  /// <summary>
  /// The number of <paramref name="key"/> kept underneath its formula, as
  /// shown: what shows when none of its cases holds. NaN for a gradient's numbers.
  /// </summary>
  public double Plain(string key)
  {
    OverlayShapeSettings shape = _context.Shape;
    float scale = _context.ElementScale;
    return Kind(key, out int index) switch
    {
      ShapeMeasures.WidthKey => shape.Width * scale,
      ShapeMeasures.HeightKey => shape.Height * scale,
      StrokeKey => shape.StrokeWidth * scale,
      "fill-opacity" => (shape.Fill?.Alpha ?? 1) * 100,
      "stroke-opacity" => (shape.Stroke?.Alpha ?? 1) * 100,
      "radius" when index >= 0 && index < shape.Points.Count => shape.Points[index].Radius * scale,
      _ => double.NaN,
    };
  }

  private string PlainText(string key)
  {
    double plain = Plain(key);
    return double.IsNaN(plain) ? null : FormulaFieldView.Number(plain) + Unit(key);
  }

  private static string Unit(string key)
  {
    string kind = Kind(key, out _);
    return kind.EndsWith("opacity") || kind.EndsWith("position") ? "%"
      : kind.EndsWith("angle") ? "°"
      : " px";
  }

  // ---- Reading and writing ----

  public OverlayVariable Read(string key)
  {
    OverlayVariable gradient = _gradient.Read(Kind(key, out _));
    if (gradient != null)
      return gradient;
    OverlayShapeSettings shape = _context.Shape;
    float scale = _context.ElementScale;
    switch (Kind(key, out int index))
    {
      case ShapeMeasures.WidthKey:
        return Number(shape.WidthFormula, shape.Width * scale);
      case ShapeMeasures.HeightKey:
        return Number(shape.HeightFormula, shape.Height * scale);
      case StrokeKey:
        return Number(shape.StrokeWidthFormula, shape.StrokeWidth * scale);
      case "fill-opacity":
        return OpacityFieldView.Read(shape.Fill);
      case "stroke-opacity":
        return OpacityFieldView.Read(shape.Stroke);
      case "radius" when index >= 0 && index < shape.Points.Count:
        return Number(shape.Points[index].RadiusFormula, shape.Points[index].Radius * scale);
      default:
        return new OverlayVariable();
    }
  }

  private static OverlayVariable Number(string formula, float shown) =>
    string.IsNullOrEmpty(formula)
      ? OverlayVariable.FromNumber(Mathf.Round(shown * 10) / 10)
      : OverlayVariable.Parse(formula);

  // A plain number replaces any formula; a formula keeps the number underneath.
  private void Write(string key, OverlayVariable formula)
  {
    _history.Remember(key);
    if (_gradient.Write(Kind(key, out _), formula))
      return;
    OverlayShapeSettings shape = _context.Shape;
    float scale = _context.ElementScale;
    bool plain = formula.IsPlainNumber(out double value);
    string text = plain ? "" : formula.Serialize();
    float number = (float)value;
    switch (Kind(key, out int index))
    {
      case ShapeMeasures.WidthKey:
        if (plain)
          shape.Width = Mathf.Clamp(number, 1, MaximumSize) / scale;
        shape.WidthFormula = text;
        break;
      case ShapeMeasures.HeightKey:
        if (plain)
          shape.Height = Mathf.Clamp(number, 1, MaximumSize) / scale;
        shape.HeightFormula = text;
        break;
      case StrokeKey:
        if (plain)
          shape.StrokeWidth = Mathf.Clamp(number, 0, MaximumStroke) / scale;
        shape.StrokeWidthFormula = text;
        break;
      case "fill-opacity":
        shape.Fill = OpacityFieldView.Write(shape.Fill, formula);
        break;
      case "stroke-opacity":
        shape.Stroke = OpacityFieldView.Write(shape.Stroke, formula);
        break;
      case "radius" when index >= 0 && index < shape.Points.Count:
        if (plain)
        {
          List<NVector2> corners = new();
          OverlayShapeGeometry.Corners(shape, corners);
          shape.Points[index].Radius = OverlayShapeGeometry.ClampRadius(corners, index, number / scale);
        }
        shape.Points[index].RadiusFormula = text;
        break;
    }
  }

  /// <summary>What a key edits, before its colon; a corner's index after it.</summary>
  public static string Kind(string key, out int index)
  {
    index = -1;
    int colon = key?.IndexOf(':') ?? -1;
    if (colon < 0)
      return key;
    int.TryParse(key.Substring(colon + 1), out index);
    return key.Substring(0, colon);
  }

  // ---- The dock ----

  /// <summary>Leaves the open number, saying so when what was typed had to be brought into range.</summary>
  public void Finish()
  {
    if (Field == null)
      return;
    string key = Field;
    OverlayVariable typed = _editing.Root;
    Field = null;
    _editing.Deactivate();
    if (typed == null || !typed.IsPlainNumber(out double number) || _context.Frame == null)
      return;
    string kind = Kind(key, out _);
    if ((kind == ShapeMeasures.WidthKey || kind == ShapeMeasures.HeightKey) && (number < 1 || number > MaximumSize))
      _editor.Notice(L("너비와 높이는 1–10000px 사이로 맞췄어요", "Width and height were kept between 1 and 10000px"));
    else if (kind == StrokeKey && (number < 0 || number > MaximumStroke))
      _editor.Notice(L("테두리 두께는 0–100px 사이로 맞췄어요", "Border width was kept between 0 and 100px"));
    else if (kind.EndsWith("opacity") && OpacityFieldView.OutOfRange(typed))
      _editor.Notice(L("불투명도는 0–100% 사이로 맞췄어요", "Opacity was kept between 0 and 100%"));
    else if (kind.EndsWith("position") && (number < 0 || number > 100))
      _editor.Notice(L("색 위치는 0–100% 사이로 맞췄어요", "The color's position was kept between 0 and 100%"));
    else if (kind == "radius" && !Read(key).SameAs(OverlayVariable.FromNumber(number)))
      _editor.Notice(L("둥글기는 이 꼭짓점에 들어가는 만큼만 적용돼요", "Rounding was kept to what fits this corner"));
  }

  public void KeypadPress(char key)
  {
    if (_context.Frame == null)
      return;
    if (key == '\b')
      _editing.Backspace();
    else
      _editing.Press(key);
    _editor.Render();
  }

  public void FinishEditing()
  {
    if (_editing.Active && _editing.Problem != null)
    {
      _editor.Notice(FormulaRefusals.Explain(_editing.Problem));
      return;
    }
    _editor.CloseOpen();
    _editor.Render();
  }

  /// <summary>A value picked in the dock joins or replaces the open number's formula.</summary>
  public void PickValue(OverlayValueDefinition value)
  {
    if (_context.Frame == null)
      return;
    if (!_editing.Active)
    {
      _editor.Notice(L("먼저 값을 넣을 숫자를 고르세요", "Pick the number for it first"), false);
      return;
    }
    if (_editing.Drop(_editing.Slot, value) == FormulaDropResult.Refused)
      Refuse(value);
    _editor.Render();
  }

  /// <summary>Says why <paramref name="value"/> can't go where it was put.</summary>
  public void Refuse(OverlayValueDefinition value) => _editor.Notice(FormulaRefusals.Why(value, _editing));

  /// <summary>The open number's name in the dock.</summary>
  public string Name(string key) =>
    _gradient.Name(Kind(key, out _))
    ?? Kind(key, out int index) switch
    {
      ShapeMeasures.WidthKey => L("너비", "Width"),
      ShapeMeasures.HeightKey => L("높이", "Height"),
      StrokeKey => L("테두리 두께", "Border width"),
      "fill-opacity" => L("채우기 불투명도", "Fill opacity"),
      "stroke-opacity" => L("테두리 불투명도", "Border opacity"),
      _ => L("꼭짓점 ", "Corner ") + (index + 1) + L(" 둥글기", " rounding"),
    };

  /// <summary>The open number's result as the dock shows it, with its unit.</summary>
  public string ResultText(string key)
  {
    OverlayVariable formula = _editing.Active ? _editing.Root : Read(key);
    if (formula.IsEmpty)
      return null;
    return FormulaFieldView.Number(Result(formula, Plain(key))) + Unit(key);
  }

  /// <summary>What <paramref name="formula"/> works out to with the editor's examples; <paramref name="fallback"/> when nothing (no case holds).</summary>
  public double Result(OverlayVariable formula, double fallback = double.NaN) =>
    formula.TryNumber(_values.WithSamples, out double result, out _) ? result : fallback;

  /// <summary>What the open number of <paramref name="key"/> shows: the formula being typed, or the saved one.</summary>
  public OverlayVariable Shown(string key) => Field == key && _editing.Active ? _editing.Root : Read(key);

  /// <summary>A number shows its result; one worked out from a formula reads in the accent.</summary>
  public void Show(FormulaFieldView field, string key)
  {
    OverlayVariable formula = Shown(key);
    bool computed = !formula.IsPlainNumber(out double plain) && !formula.IsEmpty;
    double shown =
      computed ? Result(formula, Plain(key))
      : formula.IsEmpty ? double.NaN
      : plain;
    field.Set(FormulaFieldView.Number(shown), computed, Field == key);
  }

  /// <summary>The bar's border width, like the other numbers.</summary>
  public void ShowStrokeWidth()
  {
    OverlayVariable stroke = Shown(StrokeKey);
    _strokeWidth.ShowField(
      stroke,
      stroke.IsEmpty ? "" : FormulaFieldView.Number(Result(stroke)) + " px",
      Field == StrokeKey
    );
  }
}
