using System;
using Element.Configuration.Overlay;
using Element.Domain.Overlay.Gradient;
using Element.Domain.Overlay.Variables;
using Element.Presentation.Overlay.Text;
using Element.Presentation.Overlay.Values;
using Element.Presentation.OverlayEditor.Parts.EditorFrame;
using Element.Presentation.OverlayEditor.Parts.Formula;
using Element.Presentation.Ui.ColorPicker;
using Element.Presentation.Ui.Theme;
using Element.Presentation.Ui.Widgets;
using UnityEngine;
using static Element.Presentation.Ui.Theme.UiLocale;

namespace Element.Presentation.OverlayEditor.Parts.Gradient;

/// <summary>
/// The gradient of the color an editor's picker shows: handles over the
/// preview with the angle beside them, the picked color's position in the
/// picker, and how a stop and the angle read and write as the dock's
/// numbers. The editor says which color that is and where its box sits.
/// </summary>
internal sealed class GradientEditing
{
  private readonly OverlayValues _values;
  private readonly Func<OverlayColorSettings> _color;
  private readonly Func<int, float> _shownPosition;
  private readonly Func<int, float> _shownAlpha;
  private readonly Func<int, Color> _stopShown;
  private UiColorPicker _picker;

  /// <param name="color">The color the picker shows.</param>
  public GradientEditing(OverlayValues values, Func<OverlayColorSettings> color)
  {
    _values = values;
    _color = color;
    _shownPosition = index => ShownStop(index, true);
    _shownAlpha = index => ShownStop(index, false);
    _stopShown = StopShown;
  }

  public GradientHandles Handles { get; private set; }

  /// <summary>The picked color's position, over the picker's color bar.</summary>
  public FormulaFieldView Position { get; private set; }

  /// <summary>A color added on the line starts in the highlight.</summary>
  public static string NewStopHex => "#" + ColorUtility.ToHtmlStringRGB(UiTheme.Accent);

  /// <summary>The handles over the preview, with the editor's undo and redraw.</summary>
  public void BuildHandles(
    EditorFramePanel screen,
    Action activateAngle,
    bool reduceMotion,
    Action began,
    Action changed,
    Action<float> added
  )
  {
    Handles = new GradientHandles(screen.Ui, screen.Frame, screen.ToFrame, activateAngle, reduceMotion);
    Handles.Began = began;
    Handles.Changed = changed;
    Handles.Picked = index => _picker.Gradient.SelectStop(index);
    Handles.Added = added;
  }

  /// <summary>
  /// The position number in <paramref name="picker"/>, and what the picker asks
  /// of the gradient: the box it spans and its stops as shown.
  /// </summary>
  public void BuildPicker(
    UiFactory ui,
    UiColorPicker picker,
    Action activatePosition,
    bool reduceMotion,
    Func<Vector2> box,
    Action stopSelected
  )
  {
    _picker = picker;
    Position = new FormulaFieldView(
      ui,
      picker.Gradient.PositionSlot,
      "Color position",
      "%",
      activatePosition,
      reduceMotion,
      L("위치", "Position")
    );
    picker.Gradient.HasPosition = true;
    picker.Gradient.GradientBox = box;
    picker.Gradient.ShownPosition = _shownPosition;
    picker.Gradient.ShownAlpha = _shownAlpha;
    picker.Gradient.StopSelected = stopSelected;
  }

  /// <summary>The picked color of <paramref name="color"/>'s gradient; null without one.</summary>
  public OverlayGradientStop PickedStop(OverlayColorSettings color)
  {
    int index = _picker?.Gradient.SelectedStop ?? -1;
    return color != null && color.IsGradient && index >= 0 && index < color.Gradient.Stops.Count
      ? color.Gradient.Stops[index]
      : null;
  }

  /// <summary>Shows a color just added at <paramref name="index"/> of <paramref name="color"/>, picked.</summary>
  public void Pick(OverlayColorSettings color, int index)
  {
    _picker.Refresh(color);
    _picker.Gradient.SelectStop(index);
  }

  /// <summary>
  /// Shows <paramref name="color"/> in the open picker. False, with the
  /// handles hidden, when the picker is closed or the color has no gradient.
  /// </summary>
  public bool Refresh(OverlayColorSettings color)
  {
    // While the picker shows a case's color, the gradient isn't what it edits.
    if (!_picker.Visible || color == null || _picker.ShowsOther)
    {
      Handles.Hide();
      return false;
    }
    _picker.Refresh(color);
    if (!color.IsGradient)
    {
      Handles.Hide();
      return false;
    }
    return true;
  }

  /// <summary>
  /// The handles of <paramref name="color"/> over a box whose top left is
  /// <paramref name="topLeft"/> (frame units), <paramref name="scale"/> frame
  /// units per box unit. Set the angle's and position's numbers, then <see cref="PlaceNumbers"/>.
  /// </summary>
  public void ShowHandles(OverlayColorSettings color, Vector2 topLeft, float scale, float width, float height) =>
    Handles.Show(color, _picker.Gradient.SelectedStop, topLeft, scale, width, height, _shownPosition, _stopShown);

  /// <summary>The angle beside the line and the position in the picker's numbers, once they are set.</summary>
  public void PlaceNumbers()
  {
    Handles.PlaceAngle();
    Position.Fill(L("위치", "Position"));
  }

  /// <summary>The dock's name for a gradient number; null for others.</summary>
  public string Name(GradientNumber number, OverlayColorSettings color)
  {
    int rank =
      color != null && color.IsGradient
        ? OverlayGradientEdits.Order(color.Gradient).IndexOf(_picker.Gradient.SelectedStop) + 1
        : 1;
    return number switch
    {
      GradientNumber.Position => L(rank + "번째 색 위치", "Color " + rank + " position"),
      GradientNumber.Angle => L("그라디언트 각도", "Gradient angle"),
      GradientNumber.Opacity when PickedStop(color) != null => L(
        rank + "번째 색 불투명도",
        "Color " + rank + " opacity"
      ),
      _ => null,
    };
  }

  /// <summary>A stop's position or opacity in percent: its formula, or the plain number to one decimal.</summary>
  public static OverlayVariable ReadStop(OverlayGradientStop stop, bool position)
  {
    if (stop == null)
      return new OverlayVariable();
    string formula = position ? stop.PositionFormula : stop.AlphaFormula;
    if (!string.IsNullOrEmpty(formula))
      return OverlayVariable.Parse(formula);
    float plain = position ? stop.Position : stop.Alpha;
    return OverlayVariable.FromNumber(Mathf.Round(Mathf.Clamp01(plain) * 1000) / 10);
  }

  /// <summary>A plain percent replaces the stop's formula; a formula keeps the number underneath.</summary>
  public static void WriteStop(OverlayGradientStop stop, bool position, OverlayVariable formula)
  {
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
  }

  /// <summary>The angle in degrees over a <paramref name="width"/> by <paramref name="height"/> box: its formula, or the plain number.</summary>
  public static OverlayVariable ReadAngle(OverlayColorSettings color, float width, float height)
  {
    if (color == null || !color.IsGradient)
      return new OverlayVariable();
    if (!string.IsNullOrEmpty(color.Gradient.AngleFormula))
      return OverlayVariable.Parse(color.Gradient.AngleFormula);
    return OverlayVariable.FromNumber(Mathf.Round(OverlayGradientMath.Angle(color.Gradient, width, height)) % 360);
  }

  /// <summary>A plain angle turns the gradient over the box; a formula keeps the turn underneath.</summary>
  public static void WriteAngle(OverlayColorSettings color, OverlayVariable formula, float width, float height)
  {
    if (formula.IsPlainNumber(out double degrees))
    {
      OverlayGradientMath.SetAngle(color.Gradient, (float)(((degrees % 360) + 360) % 360), width, height);
      color.Gradient.AngleFormula = string.Empty;
    }
    else
      color.Gradient.AngleFormula = formula.Serialize();
  }

  // A stop's position or opacity (0–1) with its formula worked out on the samples.
  private float ShownStop(int index, bool position)
  {
    OverlayColorSettings color = _color();
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
    OverlayColorSettings color = _color();
    OverlayGradientStop stop = color.Gradient.Stops[index];
    Color shown = color.Highlight ? UiTheme.Accent : OverlayTextComposer.ParseHex(stop.Hex);
    shown.a = ShownStop(index, false);
    return shown;
  }
}
