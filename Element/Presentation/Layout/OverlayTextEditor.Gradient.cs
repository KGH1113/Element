using System.Collections.Generic;
using Element.Configuration;
using Element.Domain.Elements;
using Element.Presentation.Overlay.Elements;
using Element.Presentation.Overlay.Shared;
using UnityEngine;
using static Element.Presentation.Settings.SettingsLocale;

namespace Element.Presentation.Layout;

// Gradients in text: the preview colors gradient runs across the whole
// text's box as the game does, and while the picker shows a gradient its
// handles sit over that box. The picked color's position, its opacity and
// the angle are numbers the dock types, like the size.
internal sealed partial class OverlayTextEditor
{
  private const string PositionKey = "position",
    AngleKey = "angle";

  private readonly List<OverlayColorSettings> _composedGradients = new();
  private readonly OverlayGradientFormulas _swatchFormulas = new();
  private readonly OverlayResolvedGradient _swatchGradient = new();
  private OverlayTextGradients _previewGradients;
  private OverlayGradientHandles _handles;
  private OverlayFormulaField _position;

  // The range's color as the handles move it; written to the range on each move.
  private OverlayColorSettings _handleColor;

  private static bool GradientKey(string key) => key == PositionKey || key == AngleKey;

  private void BuildGradient()
  {
    _previewGradients = _preview.gameObject.AddComponent<OverlayTextGradients>();
    _previewGradients.Initialize(_preview);
    _handles = new OverlayGradientHandles(_ui, _frame, _screen.ToFrame, () => ActivateField(AngleKey), _reduceMotion());
    _handles.Began = () => Remember(false);
    _handles.Changed = () =>
    {
      if (_handleColor == null)
        return;
      SetRangeColor(_handleColor.Clone());
      Render();
    };
    _handles.Picked = index => _picker.SelectStop(index);
    _handles.Added = AddStopAt;
  }

  private void BuildGradientPicker()
  {
    _position = new OverlayFormulaField(_ui, _picker.PositionSlot, "Color position", "%", () => ActivateField(PositionKey), _reduceMotion());
    _picker.GradientBox = () =>
      OverlayTextGradients.Box(_preview.textInfo, out Rect box) ? box.size : Vector2.one;
    _picker.ShownPosition = index => ShownStop(index, true);
    _picker.ShownAlpha = index => ShownStop(index, false);
    _picker.StopSelected = () =>
    {
      // The dock was typing the previous color's number.
      if (_fieldSlot == PositionKey || _fieldSlot == OpacityKey)
        ClosePopovers();
      Render();
    };
  }

  // The range's color, or the color typing will use.
  private void SetRangeColor(OverlayColorSettings color)
  {
    Range(out int start, out int end);
    if (start < end)
      _document.SetColor(start, end, color);
    else
      _pendingColor = color;
  }

  private void AddStopAt(float position)
  {
    OverlayColorSettings color = RangeColor()?.Clone();
    if (color == null || !color.IsGradient)
      return;
    Remember(false);
    int added = OverlayGradientEdits.AddStop(color, position, "#" + ColorUtility.ToHtmlStringRGB(OverlayTheme.Accent));
    if (added < 0)
      return;
    SetRangeColor(color);
    _picker.Refresh(color);
    _picker.SelectStop(added);
    Render();
  }

  // ---- The preview ----

  // Gradient runs are written in a marker color the preview paints after
  // layout; one slot per distinct gradient.
  private void BeginComposeGradients(Color highlight)
  {
    _composedGradients.Clear();
    _previewGradients.Clear();
    _previewGradients.SetSource(_values.WithSamples, highlight);
  }

  private Color PaintColor(OverlayColorSettings color, Color highlight, IOverlayNumberSource samples)
  {
    if (color != null && color.IsGradient)
    {
      for (int i = 0; i < _composedGradients.Count; i++)
        if (_composedGradients[i].SameAs(color))
          return OverlayTextGradients.Marker(i);
      _composedGradients.Add(color);
      return OverlayTextGradients.Marker(_previewGradients.Add(color));
    }
    return OverlayTextGradients.AvoidMarker(
      OverlayTextComposer.Resolve(color, highlight, OverlayFormulaText.Compile(color?.AlphaFormula), samples)
    );
  }

  // The range's gradient in the bar's color button.
  private void ShowSwatchGradient(OverlayColorSettings color)
  {
    _swatchFormulas.Compile(color);
    bool gradient = _swatchFormulas.Resolve(color, OverlayTheme.Accent, _values.WithSamples, 1, 1, _swatchGradient);
    _colorButton.SwatchGradient = gradient ? _swatchGradient : null;
  }

  // ---- The handles ----

  private void ShowGradient()
  {
    OverlayColorSettings color = RangeColor();
    if (!_picker.Visible || color == null)
    {
      _handles.Hide();
      return;
    }
    _picker.Refresh(color);
    if (!color.IsGradient || !OverlayTextGradients.Box(_preview.textInfo, out Rect box))
    {
      _handles.Hide();
      return;
    }
    // The handles keep the color they move until they let go.
    if (!_handles.Dragging || _handleColor == null)
      _handleColor = color.Clone();
    Vector2 topLeft = InFrame(new Vector3(box.xMin, box.yMax, 0));
    Vector2 bottomRight = InFrame(new Vector3(box.xMax, box.yMin, 0));
    float scale = (bottomRight.x - topLeft.x) / Mathf.Max(.001f, box.width);
    _handles.Show(
      _handleColor,
      _picker.SelectedStop,
      topLeft,
      scale,
      box.width,
      box.height,
      index => ShownStop(index, true),
      StopShown
    );
    ShowNumber(_handles.Angle, AngleKey, ReadAngle);
    _handles.PlaceAngle();
    ShowNumber(_position, PositionKey, () => ReadStop(true));
    _picker.PlacePosition(_position.Width);
    _position.Place(0, 0);
  }

  // A point of the text (its local space) in frame units from the top left,
  // through the transforms alone, so it holds whatever camera draws the canvas.
  private Vector2 InFrame(Vector3 local)
  {
    Vector3 point = _frame.InverseTransformPoint(_preview.rectTransform.TransformPoint(local));
    Rect frame = _frame.rect;
    return new Vector2(point.x - frame.xMin, frame.yMax - point.y);
  }

  private void ShowNumber(OverlayFormulaField field, string key, System.Func<OverlayFormula> read)
  {
    bool active = _fieldSlot == key;
    OverlayFormula formula = active && _editing.Active ? _editing.Working.Trimmed() : read();
    bool computed = !formula.IsEmpty && !formula.IsPlainNumber(out _);
    field.Set(formula.IsEmpty ? "—" : OverlayFormulaField.Number(Result(formula)), computed, active);
  }

  private float ShownStop(int index, bool position)
  {
    OverlayColorSettings color = RangeColor();
    if (color == null || !color.IsGradient || index < 0 || index >= color.Gradient.Stops.Count)
      return position ? 0 : 1;
    OverlayGradientStop stop = color.Gradient.Stops[index];
    string formula = position ? stop.PositionFormula : stop.AlphaFormula;
    float plain = position ? stop.Position : stop.Alpha;
    double percent = OverlayFormulaText.Resolve(OverlayFormulaText.Compile(formula), _values.WithSamples, plain * 100);
    return double.IsNaN(percent) || double.IsInfinity(percent) ? plain : Mathf.Clamp01((float)percent / 100f);
  }

  private Color StopShown(int index)
  {
    OverlayColorSettings color = RangeColor();
    OverlayGradientStop stop = color.Gradient.Stops[index];
    Color shown = color.Highlight ? OverlayTheme.Accent : OverlayTextComposer.ParseHex(stop.Hex);
    shown.a = ShownStop(index, false);
    return shown;
  }

  // ---- The numbers ----

  private OverlayGradientStop PickedStop(OverlayColorSettings color)
  {
    int index = _picker?.SelectedStop ?? -1;
    return color != null && color.IsGradient && index >= 0 && index < color.Gradient.Stops.Count
      ? color.Gradient.Stops[index]
      : null;
  }

  private OverlayFormulaSlot GradientSlot() =>
    _fieldSlot switch
    {
      PositionKey => new OverlayFormulaSlot(PositionKey, false, () => ReadStop(true), formula => WriteStop(true, formula)),
      AngleKey => new OverlayFormulaSlot(AngleKey, false, ReadAngle, WriteAngle),
      _ => null,
    };

  // A stop's position or opacity in percent: its formula, or the plain number.
  private OverlayFormula ReadStop(bool position)
  {
    OverlayGradientStop stop = PickedStop(RangeColor());
    if (stop == null)
      return new OverlayFormula();
    string formula = position ? stop.PositionFormula : stop.AlphaFormula;
    if (!string.IsNullOrEmpty(formula))
      return OverlayFormula.Parse(formula);
    float plain = position ? stop.Position : stop.Alpha;
    return OverlayFormula.FromNumber(Mathf.Round(Mathf.Clamp01(plain) * 1000) / 10);
  }

  private void WriteStop(bool position, OverlayFormula formula)
  {
    RememberFormula(position ? PositionKey : OpacityKey);
    OverlayColorSettings color = RangeColor()?.Clone();
    OverlayGradientStop stop = PickedStop(color);
    if (stop == null)
      return;
    bool plain = formula.IsPlainNumber(out double percent);
    string text = plain ? "" : formula.Serialize();
    if (position)
    {
      if (plain)
        stop.Position = Mathf.Clamp01((float)percent / 100f);
      stop.PositionFormula = text;
    }
    else
    {
      if (plain)
        stop.Alpha = Mathf.Clamp01((float)percent / 100f);
      stop.AlphaFormula = text;
    }
    OverlayGradientEdits.Sync(color);
    SetRangeColor(color);
  }

  private OverlayFormula ReadAngle()
  {
    OverlayColorSettings color = RangeColor();
    if (color == null || !color.IsGradient)
      return new OverlayFormula();
    if (!string.IsNullOrEmpty(color.Gradient.AngleFormula))
      return OverlayFormula.Parse(color.Gradient.AngleFormula);
    Vector2 box = OverlayTextGradients.Box(_preview.textInfo, out Rect rect) ? rect.size : Vector2.one;
    return OverlayFormula.FromNumber(Mathf.Round(OverlayGradientMath.Angle(color.Gradient, box.x, box.y)) % 360);
  }

  private void WriteAngle(OverlayFormula formula)
  {
    RememberFormula(AngleKey);
    OverlayColorSettings color = RangeColor()?.Clone();
    if (color == null || !color.IsGradient)
      return;
    if (formula.IsPlainNumber(out double degrees))
    {
      Vector2 box = OverlayTextGradients.Box(_preview.textInfo, out Rect rect) ? rect.size : Vector2.one;
      OverlayGradientMath.SetAngle(color.Gradient, (float)(((degrees % 360) + 360) % 360), box.x, box.y);
      color.Gradient.AngleFormula = string.Empty;
    }
    else
      color.Gradient.AngleFormula = formula.Serialize();
    SetRangeColor(color);
  }

  // The dock's name for a gradient number; null for others.
  private string GradientFieldName()
  {
    OverlayColorSettings color = RangeColor();
    int rank =
      color != null && color.IsGradient
        ? OverlayGradientEdits.Order(color.Gradient).IndexOf(_picker.SelectedStop) + 1
        : 1;
    return _fieldSlot switch
    {
      PositionKey => L(rank + "번째 색 위치", "Color " + rank + " position"),
      AngleKey => L("그라디언트 각도", "Gradient angle"),
      OpacityKey when PickedStop(color) != null => L(rank + "번째 색 불투명도", "Color " + rank + " opacity"),
      _ => null,
    };
  }
}
