using System.Collections.Generic;
using Element.Configuration;
using Element.Presentation.Layout.ContentEditors.Shell;
using Element.Presentation.Overlay.Elements;
using Element.Presentation.Settings;
using UnityEngine;
using static Element.Presentation.Settings.SettingsLocale;
using NVector2 = System.Numerics.Vector2;
using Element.Configuration.Overlay;
using Element.Domain.Overlay.Formula;
using Element.Domain.Overlay.Values;
using Element.Domain.Overlay.Shape;

namespace Element.Presentation.Layout.ContentEditors.Shape;

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

  private readonly IShapeEditorHost _host;
  private readonly OverlayValues _values;
  private readonly OverlayEditorHistory<OverlayShapeSettings> _history;
  private readonly OverlayFormulaEditing _editing = new();
  private ShapeGradient _gradient;
  private OverlayBarButton _strokeWidth;
  private OverlayFormulaSlot _slot;

  public ShapeNumbers(IShapeEditorHost host, OverlayValues values, OverlayEditorHistory<OverlayShapeSettings> history)
  {
    _host = host;
    _values = values;
    _history = history;
  }

  /// <summary>The key of the number open in the dock; null with none.</summary>
  public string Field { get; set; }

  public OverlayFormulaEditing Editing => _editing;

  public OverlayBarButton StrokeWidth => _strokeWidth;

  /// <summary>The gradient reads and writes its own numbers.</summary>
  public void Use(ShapeGradient gradient) => _gradient = gradient;

  public OverlayBarButton BuildStrokeWidth(SettingsUiFactory ui, Transform bar, bool reduceMotion) =>
    _strokeWidth = new OverlayBarButton(
      ui,
      bar,
      "Border width",
      L("테두리 두께", "Border width"),
      () => _host.Activate(StrokeKey),
      reduceMotion,
      "px",
      icon: LayoutAction.StrokeWidth
    );

  /// <summary>Points the keypad at the open number, or lets it rest.</summary>
  public void Sync() => _editing.Activate(Field == null ? null : Slot(Field));

  /// <summary>The keypad's way to read and write the number of <paramref name="key"/>.</summary>
  public OverlayFormulaSlot Slot(string key)
  {
    if (_slot == null || _slot.Key != key)
      _slot = new OverlayFormulaSlot(key, false, () => Read(key), formula => Write(key, formula));
    return _slot;
  }

  // ---- Reading and writing ----

  public OverlayFormula Read(string key)
  {
    OverlayFormula gradient = _gradient.Read(Kind(key, out _));
    if (gradient != null)
      return gradient;
    OverlayShapeSettings shape = _host.Shape;
    float scale = _host.ElementScale;
    switch (Kind(key, out int index))
    {
      case ShapeMeasures.WidthKey:
        return Number(shape.WidthFormula, shape.Width * scale);
      case ShapeMeasures.HeightKey:
        return Number(shape.HeightFormula, shape.Height * scale);
      case StrokeKey:
        return Number(shape.StrokeWidthFormula, shape.StrokeWidth * scale);
      case "fill-opacity":
        return OverlayOpacityField.Read(shape.Fill);
      case "stroke-opacity":
        return OverlayOpacityField.Read(shape.Stroke);
      case "radius" when index >= 0 && index < shape.Points.Count:
        return Number(shape.Points[index].RadiusFormula, shape.Points[index].Radius * scale);
      default:
        return new OverlayFormula();
    }
  }

  private static OverlayFormula Number(string formula, float shown) =>
    string.IsNullOrEmpty(formula)
      ? OverlayFormula.FromNumber(Mathf.Round(shown * 10) / 10)
      : OverlayFormula.Parse(formula);

  // A plain number replaces any formula; a formula keeps the number underneath.
  private void Write(string key, OverlayFormula formula)
  {
    _history.Remember(key);
    if (_gradient.Write(Kind(key, out _), formula))
      return;
    OverlayShapeSettings shape = _host.Shape;
    float scale = _host.ElementScale;
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
        shape.Fill = OverlayOpacityField.Write(shape.Fill, formula);
        break;
      case "stroke-opacity":
        shape.Stroke = OverlayOpacityField.Write(shape.Stroke, formula);
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
    OverlayFormula typed = _editing.Working?.Trimmed();
    Field = null;
    _editing.Deactivate();
    OverlayEditorFrame screen = _host.Screen;
    if (typed == null || !typed.IsPlainNumber(out double number) || screen == null)
      return;
    string kind = Kind(key, out _);
    if ((kind == ShapeMeasures.WidthKey || kind == ShapeMeasures.HeightKey) && (number < 1 || number > MaximumSize))
      screen.Notice(L("너비와 높이는 1–10000px 사이로 맞췄어요", "Width and height were kept between 1 and 10000px"));
    else if (kind == StrokeKey && (number < 0 || number > MaximumStroke))
      screen.Notice(L("테두리 두께는 0–100px 사이로 맞췄어요", "Border width was kept between 0 and 100px"));
    else if (kind.EndsWith("opacity") && OverlayOpacityField.OutOfRange(typed))
      screen.Notice(L("불투명도는 0–100% 사이로 맞췄어요", "Opacity was kept between 0 and 100%"));
    else if (kind.EndsWith("position") && (number < 0 || number > 100))
      screen.Notice(L("색 위치는 0–100% 사이로 맞췄어요", "The color's position was kept between 0 and 100%"));
    else if (kind == "radius" && !Read(key).SameAs(OverlayFormula.FromNumber(number)))
      screen.Notice(L("둥글기는 이 꼭짓점에 들어가는 만큼만 적용돼요", "Rounding was kept to what fits this corner"));
  }

  public void KeypadPress(char key)
  {
    if (_host.Screen == null)
      return;
    if (key == '\b')
      _editing.Backspace();
    else
      _editing.Press(key);
    _host.Render();
  }

  /// <summary>Starting over is its own undo step.</summary>
  public void ClearFormula()
  {
    _editing.Clear();
    _history.Break();
    _host.Render();
  }

  public void FinishEditing()
  {
    _host.CloseEditing();
    _host.Render();
  }

  /// <summary>A value picked in the dock joins or replaces the open number's formula.</summary>
  public void PickValue(OverlayValueDefinition value)
  {
    OverlayEditorFrame screen = _host.Screen;
    if (screen == null)
      return;
    if (!_editing.Active)
    {
      screen.Notice(L("먼저 값을 넣을 숫자를 고르세요", "Pick the number for it first"), false);
      return;
    }
    if (_editing.Drop(_editing.Slot, value) == OverlayDropResult.RefusedWords)
      RefuseWords(value);
    _host.Render();
  }

  public void RefuseWords(OverlayValueDefinition value) =>
    _host.Screen.Notice(
      L(
        OverlayKeypad.ValueName(value) + "은 글자라서 도형의 숫자에는 넣을 수 없어요",
        OverlayKeypad.ValueName(value) + " is words, so it can't go in a shape's number"
      )
    );

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
    OverlayFormula formula = _editing.Active ? _editing.Working.Trimmed() : Read(key);
    if (formula.IsEmpty)
      return null;
    string kind = Kind(key, out _);
    return OverlayFormulaField.Number(Result(formula))
      + (
        kind.EndsWith("opacity") || kind.EndsWith("position") ? "%"
        : kind.EndsWith("angle") ? "°"
        : " px"
      );
  }

  public double Result(OverlayFormula formula) =>
    formula.TryEvaluate(_values.WithSamples, out double result, out _) ? result : double.NaN;

  /// <summary>What the open number of <paramref name="key"/> shows: the formula being typed, or the saved one.</summary>
  public OverlayFormula Shown(string key) => Field == key && _editing.Active ? _editing.Working.Trimmed() : Read(key);

  /// <summary>A number shows its result; one worked out from a formula reads in the accent.</summary>
  public void Show(OverlayFormulaField field, string key)
  {
    OverlayFormula formula = Shown(key);
    bool computed = !formula.IsPlainNumber(out double plain) && !formula.IsEmpty;
    double shown =
      computed ? Result(formula)
      : formula.IsEmpty ? double.NaN
      : plain;
    field.Set(OverlayFormulaField.Number(shown), computed, Field == key);
  }

  /// <summary>The bar's border width, like the other numbers.</summary>
  public void ShowStrokeWidth()
  {
    OverlayFormula stroke = Shown(StrokeKey);
    bool computed = !stroke.IsEmpty && !stroke.IsPlainNumber(out _);
    _strokeWidth.Set(stroke.IsEmpty ? "—" : OverlayFormulaField.Number(Result(stroke)), computed, Field == StrokeKey);
  }
}
