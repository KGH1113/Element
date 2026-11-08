using Element.Configuration;
using Element.Domain.Elements;
using Element.Presentation.Overlay.Elements;
using Element.Presentation.Overlay.Shared;
using UnityEngine;
using static Element.Presentation.Settings.SettingsLocale;

namespace Element.Presentation.Layout;

// Gradients: the picked color's position and opacity in the picker, the
// handles and angle over the shape, and the numbers they put in the dock.
internal sealed partial class OverlayShapeEditor
{
  private OverlayGradientHandles _handles;
  private OverlayFormulaField _position;

  // The color the picker shows: the fill's or the border's.
  private OverlayColorSettings PickedColor
  {
    get => _pickerForStroke ? _shape.Stroke : _shape.Fill;
    set
    {
      if (_pickerForStroke)
        _shape.Stroke = value;
      else
        _shape.Fill = value;
    }
  }

  private string PositionKey => _pickerForStroke ? "stroke-position" : "fill-position";

  private string AngleKey => _pickerForStroke ? "stroke-angle" : "fill-angle";

  // Numbers that belong to the picker or the handles, which stay open while edited.
  private static bool PickerKey(string key)
  {
    string kind = Kind(key, out _);
    return kind.EndsWith("opacity") || kind.EndsWith("position") || kind.EndsWith("angle");
  }

  private void BuildGradient()
  {
    _handles = new OverlayGradientHandles(_ui, _frame, _screen.ToFrame, () => Activate(AngleKey), _reduceMotion());
    _handles.Began = () => Remember(null);
    _handles.Changed = () =>
    {
      _picker.Refresh(PickedColor);
      Render();
    };
    _handles.Picked = index => _picker.SelectStop(index);
    _handles.Added = AddStopAt;
  }

  private void BuildGradientPicker()
  {
    _position = new OverlayFormulaField(_ui, _picker.PositionSlot, "Color position", "%", () => Activate(PositionKey), _reduceMotion());
    _picker.GradientBox = () => new Vector2(_resolved.Width, _resolved.Height);
    _picker.ShownPosition = index => ShownStop(index, true);
    _picker.ShownAlpha = index => ShownStop(index, false);
    _picker.StopSelected = () =>
    {
      // The dock was typing the previous color's number.
      if (_field != null && (Kind(_field, out _).EndsWith("position") || Kind(_field, out _).EndsWith("opacity")))
        CloseEditing();
      Render();
    };
  }

  // A click on the gradient line adds a color there.
  private void AddStopAt(float position)
  {
    OverlayColorSettings color = PickedColor;
    if (color == null || !color.IsGradient)
      return;
    Remember(null);
    int added = OverlayGradientEdits.AddStop(color, position, "#" + ColorUtility.ToHtmlStringRGB(OverlayTheme.Accent));
    if (added < 0)
      return;
    _picker.Refresh(color);
    _picker.SelectStop(added);
    Render();
  }

  // A stop's position or opacity (0–1) with its formula worked out on the samples.
  private float ShownStop(int index, bool position)
  {
    OverlayColorSettings color = PickedColor;
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
    OverlayColorSettings color = PickedColor;
    OverlayGradientStop stop = color.Gradient.Stops[index];
    Color shown = color.Highlight ? OverlayTheme.Accent : OverlayTextComposer.ParseHex(stop.Hex);
    shown.a = ShownStop(index, false);
    return shown;
  }

  // The handles over the shape and the picked color's position pill, while
  // the picker shows a gradient.
  private void ShowGradient()
  {
    OverlayColorSettings color = PickedColor;
    if (!_picker.Visible || color == null)
    {
      _handles.Hide();
      return;
    }
    _picker.Refresh(color);
    if (!color.IsGradient)
    {
      _handles.Hide();
      return;
    }
    _handles.Show(
      color,
      _picker.SelectedStop,
      _boxTopLeft,
      _unitsToFrame,
      _resolved.Width,
      _resolved.Height,
      index => ShownStop(index, true),
      StopShown
    );
    Show(_handles.Angle, AngleKey);
    _handles.PlaceAngle();
    Show(_position, PositionKey);
    _picker.PlacePosition(_position.Width);
    _position.Place(0, 0);
  }

  // ---- The numbers ----

  private OverlayGradientStop PickedStop
  {
    get
    {
      OverlayColorSettings color = PickedColor;
      int index = _picker?.SelectedStop ?? -1;
      return color != null && color.IsGradient && index >= 0 && index < color.Gradient.Stops.Count
        ? color.Gradient.Stops[index]
        : null;
    }
  }

  private OverlayFormula ReadGradient(string kind)
  {
    OverlayColorSettings color = PickedColor;
    OverlayGradientStop stop = PickedStop;
    switch (kind)
    {
      case "fill-opacity" or "stroke-opacity" when stop != null:
        return string.IsNullOrEmpty(stop.AlphaFormula)
          ? OverlayFormula.FromNumber(Mathf.Round(Mathf.Clamp01(stop.Alpha) * 100))
          : OverlayFormula.Parse(stop.AlphaFormula);
      case "fill-position" or "stroke-position" when stop != null:
        return string.IsNullOrEmpty(stop.PositionFormula)
          ? OverlayFormula.FromNumber(Mathf.Round(Mathf.Clamp01(stop.Position) * 1000) / 10)
          : OverlayFormula.Parse(stop.PositionFormula);
      case "fill-angle" or "stroke-angle" when color != null && color.IsGradient:
        return string.IsNullOrEmpty(color.Gradient.AngleFormula)
          ? OverlayFormula.FromNumber(
            Mathf.Round(OverlayGradientMath.Angle(color.Gradient, _resolved.Width, _resolved.Height)) % 360
          )
          : OverlayFormula.Parse(color.Gradient.AngleFormula);
      default:
        return null;
    }
  }

  // A plain number replaces the formula; a formula keeps the number underneath.
  private bool WriteGradient(string kind, OverlayFormula formula)
  {
    OverlayColorSettings color = PickedColor;
    if (color == null || !color.IsGradient)
      return false;
    OverlayGradientStop stop = PickedStop;
    bool plain = formula.IsPlainNumber(out double value);
    string text = plain ? "" : formula.Serialize();
    switch (kind)
    {
      case "fill-opacity" or "stroke-opacity" when stop != null:
        if (plain)
          stop.Alpha = Mathf.Clamp01((float)value / 100f);
        stop.AlphaFormula = text;
        break;
      case "fill-position" or "stroke-position" when stop != null:
        if (plain)
          stop.Position = Mathf.Clamp01((float)value / 100f);
        stop.PositionFormula = text;
        break;
      case "fill-angle" or "stroke-angle":
        if (plain)
          OverlayGradientMath.SetAngle(
            color.Gradient,
            (float)(((value % 360) + 360) % 360),
            _resolved.Width,
            _resolved.Height
          );
        color.Gradient.AngleFormula = text;
        break;
      default:
        return false;
    }
    OverlayGradientEdits.Sync(color);
    return true;
  }

  // Arrows move the picked color 1% (10% with Shift); Delete removes it.
  private bool GradientKey(KeyCode code, bool shift)
  {
    if (_editing.Active || !_picker.Visible || !_picker.IsGradient || _picker.IsTyping)
      return false;
    if (code == KeyCode.Delete || code == KeyCode.Backspace)
    {
      Remember(null);
      _picker.RemoveSelectedStop();
      return true;
    }
    if (code != KeyCode.LeftArrow && code != KeyCode.RightArrow)
      return false;
    OverlayGradientStop stop = PickedStop;
    if (stop == null)
      return false;
    Remember(PositionKey);
    float step = (code == KeyCode.LeftArrow ? -1 : 1) * (shift ? .1f : .01f);
    stop.Position = Mathf.Clamp01(Mathf.Round((stop.Position + step) * 1000) / 1000);
    stop.PositionFormula = string.Empty;
    OverlayGradientEdits.Sync(PickedColor);
    Render();
    return true;
  }

  private string GradientFieldName(string kind, int rank) =>
    kind switch
    {
      "fill-opacity" or "stroke-opacity" when PickedStop != null => L(
        rank + "번째 색 불투명도",
        "Color " + rank + " opacity"
      ),
      "fill-position" or "stroke-position" => L(rank + "번째 색 위치", "Color " + rank + " position"),
      "fill-angle" or "stroke-angle" => L("그라디언트 각도", "Gradient angle"),
      _ => null,
    };
}
