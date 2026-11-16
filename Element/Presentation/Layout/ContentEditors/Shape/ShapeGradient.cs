using System;
using Element.Configuration.Overlay;
using Element.Domain.Overlay.Formula;
using Element.Domain.Overlay.Gradient;
using Element.Presentation.Layout.ContentEditors.Shell;
using Element.Presentation.Overlay.Values;
using Element.Presentation.Ui.ColorPicker;
using Element.Presentation.Ui.Widgets;
using UnityEngine;

namespace Element.Presentation.Layout.ContentEditors.Shape;

/// <summary>
/// A gradient fill or border: the handles and the angle over the shape, the
/// picked color's position in the picker, their numbers, and the arrow keys
/// that move the picked color.
/// </summary>
internal sealed class ShapeGradient
{
  private readonly IShapeEditorHost _host;
  private readonly ShapeColors _colors;
  private readonly ShapePreview _preview;
  private readonly OverlayEditorHistory<OverlayShapeSettings> _history;
  private readonly Action _stopSelected;
  private readonly OverlayGradientEditing _gradient;

  /// <param name="stopSelected">Another color was picked: closes its numbers if the dock had them, and redraws.</param>
  public ShapeGradient(
    IShapeEditorHost host,
    OverlayValues values,
    ShapeColors colors,
    ShapePreview preview,
    OverlayEditorHistory<OverlayShapeSettings> history,
    Action stopSelected
  )
  {
    _host = host;
    _colors = colors;
    _preview = preview;
    _history = history;
    _stopSelected = stopSelected;
    _gradient = new OverlayGradientEditing(values, () => _colors.Picked);
  }

  public OverlayGradientHandles Handles => _gradient.Handles;

  public OverlayFormulaField Position => _gradient.Position;

  public string PositionKey => _colors.ForStroke ? "stroke-position" : "fill-position";

  public string AngleKey => _colors.ForStroke ? "stroke-angle" : "fill-angle";

  /// <summary>The picked color of the picker's gradient; null without one.</summary>
  public OverlayGradientStop PickedStop => _gradient.PickedStop(_colors.Picked);

  /// <summary>Numbers that belong to the picker or the handles, which stay open while edited.</summary>
  public static bool PickerKey(string key)
  {
    string kind = ShapeNumbers.Kind(key, out _);
    return kind.EndsWith("opacity") || kind.EndsWith("position") || kind.EndsWith("angle");
  }

  public void BuildHandles(OverlayEditorFrame screen, bool reduceMotion) =>
    _gradient.BuildHandles(
      screen,
      () => _host.Activate(AngleKey),
      reduceMotion,
      () => _history.Remember((string)null),
      () =>
      {
        _colors.Picker.Refresh(_colors.Picked);
        _host.Render();
      },
      AddStopAt
    );

  public void BuildPicker(UiFactory ui, bool reduceMotion) =>
    _gradient.BuildPicker(
      ui,
      _colors.Picker,
      () => _host.Activate(PositionKey),
      reduceMotion,
      () => new Vector2(_preview.Resolved.Width, _preview.Resolved.Height),
      _stopSelected
    );

  // A click on the gradient line adds a color there.
  private void AddStopAt(float position)
  {
    OverlayColorSettings color = _colors.Picked;
    if (color == null || !color.IsGradient)
      return;
    _history.Remember((string)null);
    int added = OverlayGradientEdits.AddStop(color, position, OverlayGradientEditing.NewStopHex);
    if (added < 0)
      return;
    _gradient.Pick(color, added);
    _host.Render();
  }

  /// <summary>
  /// The handles over the shape while the picker shows a gradient; false,
  /// with them hidden, otherwise. Then <see cref="ShowNumbers"/>.
  /// </summary>
  public bool Show()
  {
    OverlayColorSettings color = _colors.Picked;
    if (!_gradient.Refresh(color))
      return false;
    _gradient.ShowHandles(
      color,
      _preview.TopLeft,
      _preview.UnitsToFrame,
      _preview.Resolved.Width,
      _preview.Resolved.Height
    );
    return true;
  }

  /// <summary>The angle beside the line and the position over the picker's bar.</summary>
  public void ShowNumbers(ShapeNumbers numbers)
  {
    numbers.Show(Handles.Angle, AngleKey);
    numbers.Show(Position, PositionKey);
    _gradient.PlaceNumbers();
  }

  // ---- The numbers ----

  /// <summary>A gradient number of <paramref name="kind"/>; null for others.</summary>
  public OverlayFormula Read(string kind)
  {
    OverlayColorSettings color = _colors.Picked;
    OverlayGradientStop stop = PickedStop;
    switch (kind)
    {
      case "fill-opacity" or "stroke-opacity" when stop != null:
        return string.IsNullOrEmpty(stop.AlphaFormula)
          ? OverlayFormula.FromNumber(Mathf.Round(Mathf.Clamp01(stop.Alpha) * 100))
          : OverlayFormula.Parse(stop.AlphaFormula);
      case "fill-position" or "stroke-position" when stop != null:
        return OverlayGradientEditing.ReadStop(stop, true);
      case "fill-angle" or "stroke-angle" when color != null && color.IsGradient:
        return OverlayGradientEditing.ReadAngle(color, _preview.Resolved.Width, _preview.Resolved.Height);
      default:
        return null;
    }
  }

  /// <summary>Writes a gradient number of <paramref name="kind"/>; false for others.</summary>
  public bool Write(string kind, OverlayFormula formula)
  {
    OverlayColorSettings color = _colors.Picked;
    if (color == null || !color.IsGradient)
      return false;
    OverlayGradientStop stop = PickedStop;
    switch (kind)
    {
      case "fill-opacity" or "stroke-opacity" when stop != null:
        OverlayGradientEditing.WriteStop(stop, false, formula);
        break;
      case "fill-position" or "stroke-position" when stop != null:
        OverlayGradientEditing.WriteStop(stop, true, formula);
        break;
      case "fill-angle" or "stroke-angle":
        OverlayGradientEditing.WriteAngle(color, formula, _preview.Resolved.Width, _preview.Resolved.Height);
        break;
      default:
        return false;
    }
    OverlayGradientEdits.Sync(color);
    return true;
  }

  /// <summary>The dock's name for a gradient number of <paramref name="kind"/>; null for others.</summary>
  public string Name(string kind) =>
    _gradient.Name(
      kind switch
      {
        "fill-opacity" or "stroke-opacity" => OverlayGradientNumber.Opacity,
        "fill-position" or "stroke-position" => OverlayGradientNumber.Position,
        "fill-angle" or "stroke-angle" => OverlayGradientNumber.Angle,
        _ => OverlayGradientNumber.None,
      },
      _colors.Picked
    );

  /// <summary>Arrows move the picked color 1% (10% with Shift); Delete removes it.</summary>
  public bool Key(KeyCode code, bool shift, bool typing)
  {
    UiColorPicker picker = _colors.Picker;
    if (typing || !picker.Visible || !picker.Gradient.IsGradient || picker.IsTyping)
      return false;
    if (code == KeyCode.Delete || code == KeyCode.Backspace)
    {
      _history.Remember((string)null);
      picker.Gradient.RemoveSelectedStop();
      return true;
    }
    if (code != KeyCode.LeftArrow && code != KeyCode.RightArrow)
      return false;
    OverlayGradientStop stop = PickedStop;
    if (stop == null)
      return false;
    _history.Remember(PositionKey);
    float step = (code == KeyCode.LeftArrow ? -1 : 1) * (shift ? .1f : .01f);
    stop.Position = Mathf.Clamp01(Mathf.Round((stop.Position + step) * 1000) / 1000);
    stop.PositionFormula = string.Empty;
    OverlayGradientEdits.Sync(_colors.Picked);
    _host.Render();
    return true;
  }
}
